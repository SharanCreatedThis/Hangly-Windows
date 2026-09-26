//
//  DirectComposition.cs
//  Hangly
//
//  Four COM calls, so the overlay's pixels can stay on the GPU.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>
/// Just enough DirectComposition to put a Win2D swap chain on a window: a device, a target
/// for the window, one visual, its content, and commit.
/// </summary>
/// <remarks>
/// <b>Why by hand.</b> The alternative is a package that wraps all of DirectComposition for
/// the sake of five methods, and a dependency the signing application has to vouch for.
/// These are called through the vtable directly, so the one thing that has to be right is
/// each method's slot. They are stated from <c>dcomp.h</c>, counting every method before
/// them including both halves of each overload — which is the trap PORTING.md warned about,
/// and why each slot is written out with what precedes it:
///
/// <list type="table">
/// <item><term>IDCompositionDevice</term><description>IUnknown (0–2), Commit 3,
/// WaitForCommitCompletion 4, GetFrameStatistics 5, CreateTargetForHwnd 6,
/// CreateVisual 7</description></item>
/// <item><term>IDCompositionTarget</term><description>IUnknown (0–2), SetRoot 3</description></item>
/// <item><term>IDCompositionVisual</term><description>IUnknown (0–2), SetOffsetX ×2,
/// SetOffsetY ×2, SetTransform ×2, SetTransformParent, SetEffect,
/// SetBitmapInterpolationMode, SetBorderMode, SetClip ×2 (3–14), SetContent 15</description></item>
/// <item><term>IDXGISwapChain2</term><description>IUnknown (0–2), IDXGIObject (3–6),
/// GetDevice 7, IDXGISwapChain (8–17), IDXGISwapChain1 (18–28), SetSourceSize 29,
/// GetSourceSize 30, SetMaximumFrameLatency 31, GetMaximumFrameLatency 32,
/// GetFrameLatencyWaitableObject 33, SetMatrixTransform 34</description></item>
/// <item><term>ICanvasResourceWrapperNative</term><description>IUnknown (0–2),
/// GetNativeResource 3 — how a Win2D swap chain gives up its DXGI one</description></item>
/// <item><term>IDirect3DDxgiInterfaceAccess</term><description>IUnknown (0–2),
/// GetInterface 3 — how a Win2D device, which is a WinRT Direct3D device, gives up its
/// DXGI one. It answers E_NOINTERFACE through the other route; measured.</description></item>
/// </list>
///
/// Every call returns an HRESULT and every failure throws, so a wrong slot or a missing
/// feature ends in the caller's fallback rather than in a half-built window.
/// </remarks>
internal static unsafe class DirectComposition
{
    private static readonly Guid IidDevice = new("C37EA93A-E7AA-450D-B16F-9746CB0407F3");
    private static readonly Guid IidDxgiDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid IidDxgiSwapChain1 = new("790A45F7-0D42-4876-983A-0A55CFE6F4AA");
    private static readonly Guid IidDxgiSwapChain2 = new("A8BE2AC4-199F-4946-B331-79599FB98DE7");
    private static readonly Guid IidResourceWrapperNative = new("5F10688D-EA55-4D55-A3B0-4DDB55C0C20A");
    private static readonly Guid IidDxgiInterfaceAccess = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    [DllImport("dcomp.dll")]
    private static extern int DCompositionCreateDevice(IntPtr dxgiDevice, in Guid iid, out IntPtr device);

    /// <summary>A composition device on the same Direct3D device Win2D draws with.</summary>
    public static IntPtr CreateDevice(object canvasDevice)
    {
        IntPtr dxgi = DxgiInterface(canvasDevice, IidDxgiDevice);
        try
        {
            Check(DCompositionCreateDevice(dxgi, IidDevice, out IntPtr device), "DCompositionCreateDevice");
            return device;
        }
        finally
        {
            Marshal.Release(dxgi);
        }
    }

    /// <summary>A target for <paramref name="window"/>, topmost within it.</summary>
    public static IntPtr CreateTarget(IntPtr device, IntPtr window)
    {
        IntPtr target;
        var call = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, IntPtr*, int>)Slot(device, 6);
        Check(call(device, window, 1, &target), "CreateTargetForHwnd");
        return target;
    }

    public static IntPtr CreateVisual(IntPtr device)
    {
        IntPtr visual;
        var call = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(device, 7);
        Check(call(device, &visual), "CreateVisual");
        return visual;
    }

    /// <summary>Shows <paramref name="canvasSwapChain"/> in <paramref name="visual"/>.</summary>
    public static void SetContent(IntPtr visual, object canvasSwapChain)
    {
        IntPtr swapChain = NativeResource(canvasSwapChain, IidDxgiSwapChain1);
        try
        {
            var call = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)Slot(visual, 15);
            Check(call(visual, swapChain), "SetContent");
        }
        finally
        {
            Marshal.Release(swapChain);
        }
    }

    /// <summary>Undoes the 1/DPI scale Win2D puts on a composition swap chain.</summary>
    /// <remarks>
    /// Win2D makes its swap chains for XAML's <c>SwapChainPanel</c>, which scales its
    /// content up by the display's DPI; to cancel that, Win2D gives the swap chain a
    /// matrix transform of 1/DPI. Under DirectComposition directly nothing scales it back,
    /// so at 200% the whole frame came out at half size in the top-left quarter of the
    /// window — measured — while the cursor was still hit-tested at full size. Identity
    /// puts one buffer pixel on one screen pixel.
    /// </remarks>
    public static void ResetScale(object canvasSwapChain)
    {
        IntPtr swapChain = NativeResource(canvasSwapChain, IidDxgiSwapChain2);
        try
        {
            float* identity = stackalloc float[6] { 1, 0, 0, 1, 0, 0 };
            var call = (delegate* unmanaged[Stdcall]<IntPtr, float*, int>)Slot(swapChain, 34);
            Check(call(swapChain, identity), "SetMatrixTransform");
        }
        finally
        {
            Marshal.Release(swapChain);
        }
    }

    public static void SetRoot(IntPtr target, IntPtr visual)
    {
        var call = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)Slot(target, 3);
        Check(call(target, visual), "SetRoot");
    }

    public static void Commit(IntPtr device)
    {
        var call = (delegate* unmanaged[Stdcall]<IntPtr, int>)Slot(device, 3);
        Check(call(device), "Commit");
    }

    /// <summary>Releases a pointer this class handed out, if there is one.</summary>
    public static void Release(ref IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.Release(pointer);
            pointer = IntPtr.Zero;
        }
    }

    /// <summary>The Direct3D or DXGI object behind a Win2D one, through Win2D's own interop.</summary>
    private static IntPtr NativeResource(object win2dObject, Guid iid)
    {
        IntPtr inspectable = WinRT.MarshalInspectable<object>.FromManaged(win2dObject);
        try
        {
            Guid wrapperIid = IidResourceWrapperNative;
            Check(Marshal.QueryInterface(inspectable, in wrapperIid, out IntPtr wrapper), "QueryInterface(ICanvasResourceWrapperNative)");
            try
            {
                IntPtr resource;
                var call = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float, Guid*, IntPtr*, int>)Slot(wrapper, 3);
                Check(call(wrapper, IntPtr.Zero, 0f, &iid, &resource), "GetNativeResource");
                return resource;
            }
            finally
            {
                Marshal.Release(wrapper);
            }
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    /// <summary>The DXGI object behind a WinRT Direct3D one.</summary>
    private static IntPtr DxgiInterface(object direct3DObject, Guid iid)
    {
        IntPtr inspectable = WinRT.MarshalInspectable<object>.FromManaged(direct3DObject);
        try
        {
            Guid accessIid = IidDxgiInterfaceAccess;
            Check(Marshal.QueryInterface(inspectable, in accessIid, out IntPtr access), "QueryInterface(IDirect3DDxgiInterfaceAccess)");
            try
            {
                IntPtr result;
                var call = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Slot(access, 3);
                Check(call(access, &iid, &result), "GetInterface");
                return result;
            }
            finally
            {
                Marshal.Release(access);
            }
        }
        finally
        {
            Marshal.Release(inspectable);
        }
    }

    private static IntPtr Slot(IntPtr instance, int index) => (*(IntPtr**)instance)[index];

    private static void Check(int hresult, string what)
    {
        if (hresult < 0)
        {
            throw new COMException($"{what} failed: 0x{hresult:X8}", hresult);
        }
    }
}
