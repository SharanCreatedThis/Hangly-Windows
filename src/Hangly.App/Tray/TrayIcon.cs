//
//  TrayIcon.cs
//  Hangly
//
//  The notification-area icon, which is this port's menu bar.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Tray;

/// <summary>One entry in the tray menu.</summary>
/// <param name="Title">What it says.</param>
/// <param name="Action">What it does. Null for a separator.</param>
/// <param name="IsChecked">Whether it carries a checkmark.</param>
/// <param name="Children">A submenu, for the rope and charm pickers.</param>
public sealed record MenuEntry(
    string Title,
    Action? Action = null,
    bool IsChecked = false,
    IReadOnlyList<MenuEntry>? Children = null)
{
    public static MenuEntry Separator { get; } = new("-");

    public bool IsSeparator => Action is null && Children is null && Title == "-";
}

/// <summary>The notification-area icon and its menu.</summary>
/// <remarks>
/// <b>Why this is hand-rolled.</b> WinUI has no tray API at all — the notification area
/// is pure Win32 — so this is a message-only window, a <c>Shell_NotifyIcon</c>
/// registration and a <c>TrackPopupMenu</c>. That is more code than the macOS original's
/// <c>MenuBarExtra</c> scene, and it buys the same three things that scene did: native
/// appearance, keyboard navigation and screen-reader support, because the menu really is
/// the system's own menu rather than a borderless window pretending to be one.
///
/// <para>The menu is rebuilt from the callback on every right-click rather than held and
/// mutated. It is built a few times a day at most, it is always read immediately after
/// it is built, and rebuilding is what makes a checkmark that disagrees with the settings
/// impossible rather than merely unlikely.</para>
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    private const int WmApp = 0x8000;
    private const int CallbackMessage = WmApp + 1;
    private const int WmRbuttonup = 0x0205;
    private const int WmLbuttonup = 0x0202;
    private const int WmCommand = 0x0111;
    private const int WmDestroy = 0x0002;

    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;

    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint MfChecked = 0x00000008;
    private const uint MfPopup = 0x00000010;

    /// <summary>Keeps a window out of Alt-Tab and off the taskbar.</summary>
    private const uint WsExToolwindow = 0x00000080;

    private const uint TpmRightbutton = 0x0002;
    private const uint TpmReturncmd = 0x0100;

    private readonly WndProc procedure;
    private readonly List<Action> commands = [];
    private IntPtr window;
    private IntPtr icon;
    private bool disposed;

    /// <summary>
    /// The message Explorer broadcasts when it has rebuilt the notification area.
    /// </summary>
    /// <remarks>
    /// Registered rather than a constant: the number is assigned by the system at
    /// runtime and is the same for every process that asks for the same string, which is
    /// how a broadcast reaches applications that were started before Explorer was.
    /// </remarks>
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");

    /// <summary>The tooltip this icon was last registered with. Needed to re-add it.</summary>
    private string tooltip;

    /// <summary>Builds the menu. Called fresh on every click, never cached.</summary>
    public Func<IReadOnlyList<MenuEntry>>? MenuBuilder { get; set; }

    /// <summary>What a left-click does, which is the same as the first menu command.</summary>
    public Action? PrimaryAction { get; set; }

    public TrayIcon(string tooltip)
    {
        this.tooltip = tooltip;
        procedure = HandleMessage;
        window = CreateMessageWindow();
        icon = LoadApplicationIcon();
        Register(tooltip);
    }

    private IntPtr CreateMessageWindow()
    {
        var wndClass = new WndClassEx
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            WndProc = Marshal.GetFunctionPointerForDelegate(procedure),
            ClassName = "HanglyTrayWindow",
        };

        // Zero means the class could not be registered, and every later call would then
        // fail for a reason that no longer mentions the class.
        if (RegisterClassEx(ref wndClass) == 0)
        {
            int error = Marshal.GetLastWin32Error();

            // 1410 is ERROR_CLASS_ALREADY_EXISTS, which is fine: the class outlives an
            // individual tray icon within the process.
            if (error != 1410)
            {
                throw new InvalidOperationException($"RegisterClassEx failed (Win32 {error}).");
            }
        }

        // A top-level window that is never shown, rather than a message-only one.
        //
        // HWND_MESSAGE is the obvious choice for a window whose whole job is receiving
        // the icon's callbacks, and it was the choice here until it cost the tray icon.
        // Broadcast messages — which is what Explorer sends as `TaskbarCreated` when it
        // has rebuilt the notification area — are delivered to **top-level windows only**,
        // and a message-only window is not one. The icon's own callbacks arrived fine,
        // because those are sent to a specific window; the one message that had to arrive
        // for the icon to come back after an Explorer restart was the one that could not.
        //
        // So: no parent, WS_EX_TOOLWINDOW to keep it out of Alt-Tab and the taskbar, and
        // no WS_VISIBLE, which leaves a window that is never shown, never sized and never
        // composited but is nonetheless top-level enough to be broadcast to.
        IntPtr created = CreateWindowEx(
            WsExToolwindow,
            "HanglyTrayWindow",

            // Named for what it is, and deliberately not "Hangly". Now that it is a
            // top-level window it can be found by anything that enumerates them, and a
            // second top-level window called "Hangly" is one an accessibility tool — or
            // this project's own UI tests — will pick up instead of the real one.
            "Hangly tray",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (created == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowEx(HWND_MESSAGE) failed (Win32 {Marshal.GetLastWin32Error()}).");
        }

        return created;
    }

    private void Register(string tooltip)
    {
        this.tooltip = tooltip;

        NotifyIconData data = NotifyIconData.Create(window, 1);
        data.Flags = NifMessage | NifIcon | NifTip;
        data.CallbackMessage = CallbackMessage;
        data.Icon = icon;
        data.Tip = tooltip;

        // Shell_NotifyIcon reports failure by returning false, not by throwing, so an
        // unchecked call is a tray icon that silently never appears.
        if (!ShellNotifyIcon(NimAdd, ref data))
        {
            throw new InvalidOperationException(
                $"Shell_NotifyIcon(NIM_ADD) failed (Win32 {Marshal.GetLastWin32Error()}); " +
                $"window={window}, icon={icon}, cbSize={data.Size}.");
        }
    }

    /// <summary>Changes the tooltip, which is where the rope's state is reported.</summary>
    public void SetTooltip(string tooltip)
    {
        this.tooltip = tooltip;

        NotifyIconData data = NotifyIconData.Create(window, 1);
        data.Flags = NifTip;
        data.Tip = tooltip;

        ShellNotifyIcon(NimModify, ref data);
    }

    /// <summary>Puts the icon back after Explorer has rebuilt the notification area.</summary>
    /// <remarks>
    /// <b>Why an application has to do this at all.</b> A notification icon belongs to
    /// the Explorer process that is drawing the notification area, not to the application
    /// that asked for it. When Explorer restarts — after a crash, or because somebody
    /// restarted it from Task Manager — every icon in the tray is gone, and the new
    /// Explorer broadcasts <c>TaskbarCreated</c> so that each application can ask again.
    /// An application that does not listen simply loses its icon for the rest of its
    /// life.
    ///
    /// <para>Which, before this existed, is what happened. Measured: Explorer restarted,
    /// Hangly still running and still drawing a charm, and no icon and no overflow
    /// chevron in the notification area. The tray menu is the only way to reach Customize,
    /// the Library, Create, About and Quit, so the app was left running with no way to
    /// use it and Task Manager as the only way out.</para>
    ///
    /// <para>Re-added rather than modified: the icon Explorer knew about is gone with the
    /// Explorer that knew about it, so <c>NIM_MODIFY</c> would be a request to change
    /// something that no longer exists. A failure here is logged rather than thrown — the
    /// new Explorer may not be ready the instant it broadcasts, and an overlay with no
    /// tray icon is still an overlay, which is the same judgement the first registration
    /// makes at startup.</para>
    /// </remarks>
    private void Readd()
    {
        NotifyIconData data = NotifyIconData.Create(window, 1);
        data.Flags = NifMessage | NifIcon | NifTip;
        data.CallbackMessage = CallbackMessage;
        data.Icon = icon;
        data.Tip = tooltip;

        if (ShellNotifyIcon(NimAdd, ref data))
        {
            Services.Diagnostics.Log("notification area rebuilt; tray icon re-added");
            return;
        }

        Services.Diagnostics.Log(
            $"notification area rebuilt but the tray icon could not be re-added " +
            $"(Win32 {Marshal.GetLastWin32Error()})");
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        // Not a constant, so it cannot be a case label. Checked first because a
        // rebuilt notification area is the one message that must never be missed.
        if (message == taskbarCreated && !disposed)
        {
            Readd();
            return IntPtr.Zero;
        }

        switch (message)
        {
            case CallbackMessage:
            {
                int mouse = (int)(lParam.ToInt64() & 0xFFFF);
                if (mouse is WmRbuttonup or WmLbuttonup)
                {
                    ShowMenu();
                }

                return IntPtr.Zero;
            }

            case WmCommand:
            {
                int command = (int)(wParam.ToInt64() & 0xFFFF);
                if (command > 0 && command <= commands.Count)
                {
                    commands[command - 1]();
                }

                return IntPtr.Zero;
            }

            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;

            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
        }
    }

    private void ShowMenu()
    {
        IReadOnlyList<MenuEntry> entries = MenuBuilder?.Invoke() ?? [];
        commands.Clear();

        IntPtr menu = CreatePopupMenu();
        Populate(menu, entries);

        GetCursorPos(out Point cursor);

        // Required before TrackPopupMenu, and the reason a tray menu built without it
        // stays on screen after the user clicks elsewhere: the menu dismisses on losing
        // activation, and a message-only window has none until it is given some.
        SetForegroundWindow(window);

        // TPM_RETURNCMD dispatches here rather than posting WM_COMMAND, which keeps the
        // whole interaction synchronous and inside this method.
        int selected = TrackPopupMenuEx(
            menu,
            TpmRightbutton | TpmReturncmd,
            cursor.X,
            cursor.Y,
            window,
            IntPtr.Zero);

        DestroyMenu(menu);

        if (selected > 0 && selected <= commands.Count)
        {
            commands[selected - 1]();
        }
    }

    private void Populate(IntPtr menu, IReadOnlyList<MenuEntry> entries)
    {
        foreach (MenuEntry entry in entries)
        {
            if (entry.IsSeparator)
            {
                AppendMenu(menu, MfSeparator, IntPtr.Zero, string.Empty);
                continue;
            }

            if (entry.Children is { Count: > 0 } children)
            {
                IntPtr submenu = CreatePopupMenu();
                Populate(submenu, children);
                AppendMenu(menu, MfString | MfPopup, submenu, entry.Title);
                continue;
            }

            commands.Add(entry.Action ?? (() => { }));
            uint flags = MfString | (entry.IsChecked ? MfChecked : 0);
            AppendMenu(menu, flags, new IntPtr(commands.Count), entry.Title);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        NotifyIconData data = NotifyIconData.Create(window, 1);
        ShellNotifyIcon(NimDelete, ref data);

        if (icon != IntPtr.Zero)
        {
            DestroyIcon(icon);
            icon = IntPtr.Zero;
        }

        if (window != IntPtr.Zero)
        {
            DestroyWindow(window);
            window = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>The icon embedded in the running executable.</summary>
    /// <remarks>
    /// Read from the executable rather than from a file beside it. The build already
    /// embeds it through <c>ApplicationIcon</c>, and shipping a second copy as a content
    /// file gave the resource compiler two entries for one path — so there is exactly one
    /// icon now, and the tray shows the same one Explorer does by construction.
    ///
    /// <para>A null icon is survivable: the notification area falls back to a blank slot,
    /// which is worse-looking than it should be but is not a reason to refuse to run.</para>
    /// </remarks>
    private static IntPtr LoadApplicationIcon()
    {
        string? executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return IntPtr.Zero;
        }

        IntPtr large = IntPtr.Zero;
        IntPtr small = IntPtr.Zero;

        // The small icon is the one the notification area actually draws.
        if (ExtractIconEx(executable, 0, ref large, ref small, 1) > 0)
        {
            if (large != IntPtr.Zero && large != small)
            {
                DestroyIcon(large);
            }

            if (small != IntPtr.Zero)
            {
                return small;
            }
        }

        return large;
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public IntPtr WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public IntPtr SmallIcon;
    }

    /// <summary>NOTIFYICONDATAW, in full.</summary>
    /// <remarks>
    /// Declared complete rather than truncated after the fields this app uses, and that is
    /// not tidiness. The shell validates <c>cbSize</c> against the handful of struct
    /// versions it knows, and rejects anything else — by returning <c>false</c>, with no
    /// exception and no icon. A short struct therefore fails in the one way that leaves
    /// nothing to find.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint VersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIcon;

        /// <summary>A zeroed record of the right size, with no null strings in it.</summary>
        /// <remarks>
        /// The fixed-length string fields cannot be left null: marshalling a null through
        /// <c>ByValTStr</c> throws, so every one of them is an empty string even when the
        /// corresponding flag is not set.
        /// </remarks>
        public static NotifyIconData Create(IntPtr window, uint id) => new()
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = window,
            Id = id,
            Tip = string.Empty,
            Info = string.Empty,
            InfoTitle = string.Empty,
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx wndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    // EntryPoint is spelled out because the export has an underscore in it. CharSet
    // appends the W for Unicode, but nothing was ever going to guess the underscore, and
    // the mismatch surfaces as an EntryPointNotFoundException at the first call rather
    // than at build time.
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, IntPtr item, string text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr window,
        IntPtr parameters);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string file,
        int iconIndex,
        ref IntPtr largeIcon,
        ref IntPtr smallIcon,
        uint icons);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    /// <summary>
    /// Asks the system for the number a message string is known by.
    /// </summary>
    /// <remarks>
    /// Every process that registers the same string is given the same number for as long
    /// as Windows is running, which is what lets Explorer broadcast one message that
    /// every application recognises.
    /// </remarks>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);
}
