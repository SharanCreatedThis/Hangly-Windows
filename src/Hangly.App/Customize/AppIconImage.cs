//
//  AppIconImage.cs
//  Hangly
//
//  The executable's own icon, as something XAML can load.
//

using System.Drawing;

namespace Hangly.App.Customize;

/// <summary>Extracts the application icon to a PNG the About page can show.</summary>
/// <remarks>
/// The icon ships embedded in the executable and deliberately not as a second file —
/// registering it as content as well gave the PRI compiler two entries for one path.
/// The tray already reads it back out of the binary for the same reason; this does the
/// same for XAML, which loads images by URI rather than from a handle.
/// </remarks>
public static class AppIconImage
{
    private static string? cached;
    private static bool attempted;

    /// <summary>A PNG of the app icon, or null if it could not be produced.</summary>
    public static string? Path()
    {
        if (attempted)
        {
            return cached;
        }

        attempted = true;
        try
        {
            string target = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Hangly",
                "app-icon.png");

            if (File.Exists(target))
            {
                return cached = target;
            }

            string executable = Environment.ProcessPath ?? string.Empty;
            if (executable.Length == 0)
            {
                return null;
            }

            using Icon? icon = Icon.ExtractAssociatedIcon(executable);
            if (icon is null)
            {
                return null;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            using Bitmap bitmap = icon.ToBitmap();
            bitmap.Save(target, System.Drawing.Imaging.ImageFormat.Png);
            return cached = target;
        }
        catch (Exception exception)
        {
            Services.Diagnostics.Failure("app icon", exception);
            return null;
        }
    }
}
