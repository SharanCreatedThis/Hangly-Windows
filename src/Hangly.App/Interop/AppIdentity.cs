//
//  AppIdentity.cs
//  Hangly
//
//  The AppUserModelID of a window and of a process, to tell which app is which.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>Packaged apps' identities, from a window and from a process.</summary>
/// <remarks>
/// A packaged app's full-screen window can belong to <c>ApplicationFrameHost</c> with the app
/// itself drawing into it from its own process and no child window to find it by — Windows
/// 11's Media Player, measured. The frame carries the app's AppUserModelID (the taskbar
/// groups by it), and so does the app's process, so the two are matched on that. Both calls
/// are documented: <c>SHGetPropertyStoreForWindow</c> with <c>PKEY_AppUserModel_ID</c>, and
/// <c>GetApplicationUserModelId</c>. <c>IPropertyStore</c> slots: IUnknown (0–2), GetCount 3,
/// GetAt 4, GetValue 5.
/// </remarks>
internal static unsafe class AppIdentity
{
    private static readonly Guid IidPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid Format;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        public ushort Reserved1, Reserved2, Reserved3;
        public IntPtr Value;
        public IntPtr Extra;
    }

    private const ushort VtLpwstr = 31;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr window, in Guid iid, out IntPtr store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr process, ref uint length, char[]? id);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint id);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>The AppUserModelID a window declares, or null.</summary>
    public static string? OfWindow(IntPtr window)
    {
        if (SHGetPropertyStoreForWindow(window, IidPropertyStore, out IntPtr store) < 0 || store == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            var value = default(PropVariant);
            var getValue = (delegate* unmanaged[Stdcall]<IntPtr, PropertyKey*, PropVariant*, int>)(*(IntPtr**)store)[5];
            if (getValue(store, &key, &value) < 0)
            {
                return null;
            }

            string? id = value.Type == VtLpwstr ? Marshal.PtrToStringUni(value.Value) : null;
            PropVariantClear(ref value);
            return string.IsNullOrEmpty(id) ? null : id;
        }
        finally
        {
            Marshal.Release(store);
        }
    }

    /// <summary>The AppUserModelID of a packaged process, or null for an ordinary one.</summary>
    public static string? OfProcess(uint process)
    {
        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, process);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            uint length = 0;
            GetApplicationUserModelId(handle, ref length, null);
            if (length == 0 || length > 512)
            {
                return null;
            }

            var buffer = new char[length];
            return GetApplicationUserModelId(handle, ref length, buffer) == 0
                ? new string(buffer, 0, (int)length - 1)
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
