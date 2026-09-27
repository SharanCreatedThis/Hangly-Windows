//
//  DesktopLayer.cs
//  Hangly
//
//  On the Desktop: just above the desktop, below every app's windows.
//

using System.Runtime.InteropServices;
using System.Text;
using Hangly.Core.Models;

namespace Hangly.App.Overlay;

/// <summary>Holds the overlay directly above the desktop in the z-order, and says when a window covers it.</summary>
/// <remarks>
/// <b>Not HWND_BOTTOM.</b> The desktop is itself an ordinary top-level window, full-screen
/// and opaque, at the bottom of the z-order; sending the overlay to the bottom would put it
/// underneath and the charm would vanish. So the overlay is inserted directly above the
/// window that holds the desktop icons — the lowest place that is still in front of it —
/// with an ordinary <c>SetWindowPos</c>. No reparenting and no wallpaper-window tricks:
/// nothing here depends on how a given Windows build arranges Explorer's own windows beyond
/// "one of them holds <c>SHELLDLL_DefView</c>", which is what the icons are.
///
/// <para>Re-asserted on the overlay's existing once-a-second tick, which is what brings the
/// charm back after Show desktop or an Explorer restart without a timer of its own.</para>
/// </remarks>
internal static class DesktopLayer
{
    private static IntPtr desktop;

    /// <summary>Puts <paramref name="overlay"/> directly above the desktop, if it is not already there.</summary>
    public static void HoldAboveDesktop(IntPtr overlay, uint extraFlags = 0)
    {
        if (desktop == IntPtr.Zero || !IsWindow(desktop))
        {
            desktop = FindDesktop();
            if (desktop == IntPtr.Zero)
            {
                return;
            }
        }

        // Out of the topmost band first: a topmost window placed after a normal one is
        // not reliably demoted, and a charm meant to be behind windows that is still
        // topmost is the one outcome worse than doing nothing.
        if ((GetWindowLongPtr(overlay, GwlExstyle).ToInt64() & WsExTopmost) != 0)
        {
            SetWindowPos(overlay, HwndNotopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate);
        }

        // Already in place when nothing that can be seen lies between the desktop and the
        // overlay. Windows keeps a crowd of hidden and cloaked windows down here, and they
        // cover nothing.
        for (IntPtr window = GetWindow(desktop, GwHwndprev); window != IntPtr.Zero; window = GetWindow(window, GwHwndprev))
        {
            if (window == overlay)
            {
                if (extraFlags != 0)
                {
                    SetWindowPos(overlay, IntPtr.Zero, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate | SwpNozorder | extraFlags);
                }

                return;
            }

            if (IsShowing(window))
            {
                break;
            }
        }

        // Directly above the desktop, or as close as Windows allows. "After" in SetWindowPos
        // means below, so each attempt puts the overlay under a window that is above the
        // desktop. Some of the windows just above it belong to processes a normal app may
        // not place itself against — the attempt fails with access denied, measured in the
        // VM — and the next window up is then the nearest place that is allowed.
        int attempts = 0;
        for (IntPtr window = GetWindow(desktop, GwHwndprev);
             window != IntPtr.Zero && window != overlay && attempts < MaximumAttempts;
             window = GetWindow(window, GwHwndprev), attempts++)
        {
            if (SetWindowPos(overlay, window, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNoactivate | extraFlags))
            {
                return;
            }
        }
    }

    /// <summary>Visible and not cloaked: a window that can actually be in front of something.</summary>
    private static bool IsShowing(IntPtr window) =>
        IsWindowVisible(window)
        && (DwmGetWindowAttribute(window, DwmwaCloaked, out int cloaked, sizeof(int)) != 0 || cloaked == 0);

    /// <summary>How far up from the desktop to look for a window the overlay may be placed under.</summary>
    private const int MaximumAttempts = 64;

    /// <summary>Whether another window is in front of <paramref name="overlay"/> at the cursor.</summary>
    /// <remarks>
    /// <c>WindowFromPoint</c> skips a click-through layered window, so while the overlay is
    /// letting the cursor through it reports what is underneath: the desktop when the charm
    /// shows, somebody's window when it is covered. Once the overlay holds the mouse it
    /// reports the overlay itself.
    /// </remarks>
    public static bool IsCovered(IntPtr overlay, int x, int y)
    {
        IntPtr hit = WindowFromPoint(new PointStruct { X = x, Y = y });
        if (hit == IntPtr.Zero)
        {
            return false;
        }

        IntPtr root = GetAncestor(hit, GaRoot);
        if (root == overlay || root == IntPtr.Zero)
        {
            return false;
        }

        var name = new StringBuilder(64);
        GetClassName(root, name, name.Capacity);
        return !WindowModeTable.IsDesktopClass(name.ToString());
    }

    /// <summary>The top-level window holding the desktop icons: Progman, or the WorkerW Explorer moved them into.</summary>
    private static IntPtr FindDesktop()
    {
        IntPtr progman = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
        if (progman != IntPtr.Zero && FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
        {
            return progman;
        }

        for (IntPtr worker = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
             worker != IntPtr.Zero;
             worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null))
        {
            if (FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                return worker;
            }
        }

        return progman;
    }

    private const int GwlExstyle = -20;
    private const long WsExTopmost = 0x00000008;
    private const uint GwHwndprev = 3;
    private const uint GaRoot = 2;
    private const int DwmwaCloaked = 14;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNoactivate = 0x0010;
    private const uint SwpNozorder = 0x0004;
    private static readonly IntPtr HwndNotopmost = new(-2);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(PointStruct point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
