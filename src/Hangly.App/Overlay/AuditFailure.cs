//
//  AuditFailure.cs
//  Hangly
//
//  For the recovery tests only (HANGLY_AUDIT_FAIL): makes the overlay or the notification card fail on purpose.
//  Ignored by installed copies, so nothing a user runs can be made to fail this way.
//

using System.Runtime.InteropServices;
using Hangly.App.Services;

namespace Hangly.App.Overlay;

/// <summary>Injected failures, to test that the app recovers from them (W-FRAMELOOP, W-DEVLOSTCAP, W-CARDDEVLOST).</summary>
/// <remarks>
/// <c>HANGLY_AUDIT_FAIL</c> is <c>device</c> (the overlay throws DXGI_ERROR_DEVICE_REMOVED five seconds after each build),
/// <c>error</c> (it throws an ordinary exception instead) or <c>card</c> (the next notification card to draw throws
/// DXGI_ERROR_DEVICE_REMOVED). <c>HANGLY_AUDIT_FAIL_COUNT</c> is how many overlay builds fail; the default is one.
/// </remarks>
internal static class AuditFailure
{
    private static readonly string? Mode = Updater.IsInstalled ? null : Environment.GetEnvironmentVariable("HANGLY_AUDIT_FAIL");

    private static int remaining = int.TryParse(Environment.GetEnvironmentVariable("HANGLY_AUDIT_FAIL_COUNT"), out int count) ? count : 1;

    private static long failedOverlay;

    /// <summary>Throws once per overlay build, five seconds in, while failures remain.</summary>
    public static void MaybeFailOverlay(long builtAt)
    {
        if (Mode is not ("device" or "error") || remaining <= 0 || failedOverlay == builtAt
            || System.Diagnostics.Stopwatch.GetElapsedTime(builtAt).TotalSeconds < 5)
        {
            return;
        }

        failedOverlay = builtAt;
        remaining--;
        Diagnostics.Log($"audit: failing the overlay on purpose ({Mode}; {remaining} left)");
        throw Mode == "device"
            ? new COMException("audit: device removed", unchecked((int)0x887A0005))
            : new InvalidOperationException("audit: a failure in the frame loop");
    }

    /// <summary>Throws once, the first time a card draws.</summary>
    public static void MaybeFailCard()
    {
        if (Mode != "card" || Interlocked.Exchange(ref remaining, 0) <= 0)
        {
            return;
        }

        Diagnostics.Log("audit: failing the notification card on purpose (device)");
        throw new COMException("audit: device removed", unchecked((int)0x887A0005));
    }
}
