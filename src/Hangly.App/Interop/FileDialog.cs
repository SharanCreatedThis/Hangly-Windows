//
//  FileDialog.cs
//  Hangly
//
//  Asking the user for a file, with the dialog Windows has always had.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>The "open a file" dialog.</summary>
/// <remarks>
/// <b>Not WinUI's <c>FileOpenPicker</c>, and that was not a preference.</b> The picker
/// was tried first, with <c>InitializeWithWindow</c> as the documentation requires for an
/// app with no package identity. It logged that it was opening, showed nothing, and never
/// returned — no dialog, no exception, no completion. An unpackaged WinUI app is a
/// configuration that picker does not reliably serve.
///
/// <para><c>GetOpenFileNameW</c> has no such opinion. It is the dialog every Win32
/// application has used since Windows 3.1, it does not care about package identity, and
/// it returns a string. The cost is one struct declared correctly, which is a cost this
/// project has already learned to pay attention to: <c>NOTIFYICONDATA</c> was refused
/// silently for being the wrong size, and the shell said nothing about it either.</para>
/// </remarks>
internal static class FileDialog
{
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnExplorer = 0x00080000;

    /// <summary>Leaves the process's working directory alone, which a dialog otherwise changes.</summary>
    private const int OfnNoChangeDir = 0x00000008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public string? Filter;
        public string? CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public IntPtr File;
        public int MaxFile;
        public string? FileTitle;
        public int MaxFileTitle;
        public string? InitialDirectory;
        public string? Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public string? DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public string? TemplateName;
        public IntPtr Reserved;
        public int ReservedDword;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    /// <summary>Asks for one file, and returns its path or null if the user declined.</summary>
    /// <param name="owner">The window the dialog is modal to.</param>
    /// <param name="title">What the dialog's title bar says.</param>
    /// <param name="filter">
    /// Description and pattern pairs. Written here as a normal string and separated with
    /// nulls below, because the API wants a double-null-terminated list and a C# string
    /// literal cannot hold one legibly.
    /// </param>
    public static string? OpenFile(IntPtr owner, string title, params (string Label, string Pattern)[] filter)
    {
        // Long paths exist; 260 characters is the old limit and not a safe buffer size.
        const int bufferLength = 4096;
        IntPtr buffer = Marshal.AllocHGlobal(bufferLength * sizeof(char));

        try
        {
            // A zeroed buffer is an empty initial file name.
            for (int index = 0; index < 4; index++)
            {
                Marshal.WriteByte(buffer, index, 0);
            }

            string patterns = string.Join(
                '\0',
                filter.SelectMany(entry => (string[])[entry.Label, entry.Pattern])) + "\0\0";

            var dialog = new OpenFileName
            {
                StructSize = Marshal.SizeOf<OpenFileName>(),
                Owner = owner,
                Filter = patterns,
                FilterIndex = 1,
                File = buffer,
                MaxFile = bufferLength,
                Title = title,
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir,
            };

            if (!GetOpenFileNameW(ref dialog))
            {
                // False is also what cancelling looks like, which is not an error.
                return null;
            }

            string? path = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
