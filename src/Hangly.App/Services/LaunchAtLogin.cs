//
//  LaunchAtLogin.cs
//  Hangly
//

using Microsoft.Win32;

namespace Hangly.App.Services;

/// <summary>Whether Hangly starts with the session.</summary>
/// <remarks>
/// The <c>Run</c> key under <c>HKEY_CURRENT_USER</c>, which is the per-user mechanism
/// that needs no elevation and no installer. A packaged build would use a
/// <c>StartupTask</c> instead; this one is unpackaged by design.
///
/// <para><b>Reconciled, not trusted.</b> The user can remove the entry — from Task
/// Manager's Startup tab, from Settings, or by hand — while Hangly is not running, so the
/// stored flag is corrected from the registry at launch rather than assumed. That is the
/// same rule the macOS build applies against <c>SMAppService</c>, and for the same
/// reason: a toggle that disagrees with the system is worse than no toggle.</para>
/// </remarks>
public interface ILaunchAtLogin
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

public sealed class RegistryLaunchAtLogin : ILaunchAtLogin
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hangly";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
                return key?.GetValue(ValueName) is not null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A locked-down profile can refuse this. Failing to start with the session is
            // not a reason to fail to start at all.
        }
    }
}
