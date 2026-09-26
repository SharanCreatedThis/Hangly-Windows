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

    /// <summary>An entry exists but names some other copy of Hangly.</summary>
    bool IsStale => false;

    void SetEnabled(bool enabled);
}

public sealed class RegistryLaunchAtLogin : ILaunchAtLogin
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hangly";

    /// <summary>Whether Windows will start <em>this</em> copy at sign-in.</summary>
    /// <remarks>
    /// Not merely whether a "Hangly" entry exists. It used to be, and an entry left by a
    /// copy that had since moved — a build run from a folder, then the installed one —
    /// read as enabled while Windows started a path that no longer existed, or an old
    /// build. Found in the M3 install test: the installed 1.0.2 reported launch at login
    /// on while the entry named a development build in another folder.
    /// </remarks>
    public bool IsEnabled => Entry() is string entry && NamesThisCopy(entry);

    public bool IsStale => Entry() is string entry && !NamesThisCopy(entry);

    private static string? Entry()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) as string;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool NamesThisCopy(string entry) =>
        Environment.ProcessPath is string self
        && string.Equals(entry.Trim().Trim('"'), self, StringComparison.OrdinalIgnoreCase);

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
