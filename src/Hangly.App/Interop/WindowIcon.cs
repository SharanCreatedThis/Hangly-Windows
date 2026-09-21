//
//  WindowIcon.cs
//  Hangly
//
//  Putting the app's own icon in the title bar, which WinUI does not do for itself.
//

using Microsoft.UI.Xaml;

namespace Hangly.App.Interop;

/// <summary>Gives a window the icon the executable already carries.</summary>
/// <remarks>
/// <b>WinUI ships windows with no icon at all.</b> <c>ApplicationIcon</c> embeds
/// hangly.ico in the executable, and Explorer, the taskbar and the shortcut all use it —
/// but a WinUI window's title bar does not, and Windows falls back to the generic
/// "some application" placeholder. Every Hangly window wore it.
///
/// <para><b>Taken out of the running executable rather than off disk.</b> The icon is
/// deliberately not shipped as content — <c>ApplicationIcon</c> already embeds it, and a
/// second copy in Assets is a second copy that can be stale or missing. ExtractIconEx
/// reads the embedded one, so there is exactly one icon in the build and this cannot
/// disagree with what Explorer shows.</para>
///
/// <para>Both sizes are set. <c>ICON_SMALL</c> is the title bar and the alt-tab list;
/// <c>ICON_BIG</c> is the task switcher's large tile. Setting only one leaves the other
/// on the placeholder.</para>
/// </remarks>
public static class WindowIcon
{
    private const uint SetIcon = 0x0080;
    private static readonly IntPtr Small = 0;
    private static readonly IntPtr Big = 1;

    private static IntPtr large;
    private static IntPtr small;
    private static bool loaded;

    /// <summary>Sets <paramref name="window"/>'s title-bar and task-switcher icons.</summary>
    /// <remarks>
    /// Quiet on failure. A window with the wrong icon is a blemish; a window that refuses
    /// to open because the icon could not be read is a bug, and nothing here is worth
    /// that.
    /// </remarks>
    public static void Apply(Window window)
    {
        try
        {
            if (!Load())
            {
                return;
            }

            IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (large != IntPtr.Zero)
            {
                NativeMethods.SendMessage(handle, SetIcon, Big, large);
            }

            if (small != IntPtr.Zero)
            {
                NativeMethods.SendMessage(handle, SetIcon, Small, small);
            }
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("window icon", exception);
        }
    }

    /// <summary>Reads the icon once and keeps the handles for the process's lifetime.</summary>
    /// <remarks>
    /// Never freed, on purpose: they live as long as the windows that show them, the
    /// windows live as long as the app, and three windows share two handles.
    /// </remarks>
    private static bool Load()
    {
        if (loaded)
        {
            return large != IntPtr.Zero || small != IntPtr.Zero;
        }

        loaded = true;

        string? path = Environment.ProcessPath;
        if (path is null)
        {
            return false;
        }

        NativeMethods.ExtractIconEx(path, 0, out large, out small, 1);
        return large != IntPtr.Zero || small != IntPtr.Zero;
    }
}
