//
//  CharmDropTarget.cs
//  Hangly
//
//  Letting a file be dropped on the charm itself.
//

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Hangly.App.Interop;

/// <summary>Accepts files dropped onto the overlay window.</summary>
/// <remarks>
/// <b>Why a real drop target and not <c>WM_DROPFILES</c>.</b> The shell's simple path
/// tells you a file arrived and nothing else — no enter, no leave, no chance to say
/// whether the thing being dragged is even wanted. macOS reacts while a file is held over
/// the charm and reports it separately from the drop, which is what <c>airdrop_drag_entered</c>
/// is for, so the same three moments have to exist here.
///
/// <para><b>The charm is the drop target because the charm is already the hit area.</b>
/// The overlay is click-through everywhere except the charm's grab disc, and a window
/// with <c>WS_EX_TRANSPARENT</c> receives no drops at all — the drag passes through to
/// whatever is behind it. So the same toggle that lets the charm be picked up is what
/// makes it catch a file, and nothing else about the overlay has to change. The macOS
/// documentation describes exactly this: "the charm's hit disc is already the only part
/// of the overlay that takes the mouse, so it doubles as the drop target with no change
/// to click-through elsewhere".</para>
///
/// <para>Only one file is taken from a drop of several. An import replaces one charm, and
/// there is no sensible reading of four files landing on one place.</para>
/// </remarks>
[ComVisible(true)]
[Guid("0000010E-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDataObjectNative
{
    void GetData(ref FORMATETC format, out STGMEDIUM medium);
}

/// <summary>The COM drop target Windows talks to.</summary>
[ComVisible(true)]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDropTarget
{
    [PreserveSig]
    int DragEnter(IDataObject data, uint keyState, long point, ref uint effect);

    [PreserveSig]
    int DragOver(uint keyState, long point, ref uint effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop(IDataObject data, uint keyState, long point, ref uint effect);
}

/// <summary>Reports what the shell is doing over the overlay.</summary>
[ComVisible(true)]
internal sealed class CharmDropTarget : IDropTarget
{
    private const uint DropEffectNone = 0;
    private const uint DropEffectCopy = 1;
    private const int Ok = 0;

    /// <summary>Clipboard format for a list of file names.</summary>
    private const short CfHdrop = 15;

    private readonly Action onEnter;
    private readonly Action<string> onDrop;
    private readonly Func<bool> isOverCharm;

    public CharmDropTarget(Action onEnter, Action<string> onDrop, Func<bool> isOverCharm)
    {
        this.onEnter = onEnter;
        this.onDrop = onDrop;
        this.isOverCharm = isOverCharm;
    }

    public int DragEnter(IDataObject data, uint keyState, long point, ref uint effect)
    {
        bool wanted = PathFrom(data) is not null && isOverCharm();
        effect = wanted ? DropEffectCopy : DropEffectNone;
        if (wanted)
        {
            onEnter();
        }

        return Ok;
    }

    public int DragOver(uint keyState, long point, ref uint effect)
    {
        // Re-judged on every move rather than remembered from the enter: the cursor can
        // wander off the charm and back without ever leaving the window.
        effect = isOverCharm() ? DropEffectCopy : DropEffectNone;
        return Ok;
    }

    public int DragLeave() => Ok;

    public int Drop(IDataObject data, uint keyState, long point, ref uint effect)
    {
        effect = DropEffectNone;
        if (PathFrom(data) is not string path || !isOverCharm())
        {
            return Ok;
        }

        effect = DropEffectCopy;
        onDrop(path);
        return Ok;
    }

    /// <summary>The first file name in the drag, if it is carrying any.</summary>
    private static string? PathFrom(IDataObject data)
    {
        var format = new FORMATETC
        {
            cfFormat = CfHdrop,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL,
        };

        STGMEDIUM medium = default;
        try
        {
            data.GetData(ref format, out medium);
            if (medium.unionmember == IntPtr.Zero)
            {
                return null;
            }

            uint count = NativeMethods.DragQueryFile(medium.unionmember, 0xFFFFFFFF, null, 0);
            if (count == 0)
            {
                return null;
            }

            uint length = NativeMethods.DragQueryFile(medium.unionmember, 0, null, 0);
            var buffer = new char[length + 1];
            NativeMethods.DragQueryFile(medium.unionmember, 0, buffer, length + 1);
            return new string(buffer, 0, (int)length);
        }
        catch (COMException)
        {
            // A drag that is not carrying files at all. Not an error: the answer is just
            // "nothing here", and saying so quietly is the whole of the handling.
            return null;
        }
        finally
        {
            if (medium.unionmember != IntPtr.Zero)
            {
                NativeMethods.ReleaseStgMedium(ref medium);
            }
        }
    }
}
