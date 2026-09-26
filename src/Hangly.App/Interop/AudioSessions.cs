//
//  AudioSessions.cs
//  Hangly
//
//  Which processes are playing sound right now.
//

using System.Runtime.InteropServices;

namespace Hangly.App.Interop;

/// <summary>The processes with an active audio stream on the default output, through Core Audio.</summary>
/// <remarks>
/// Used by the full-screen check to ask "is the window in front playing something?" in a
/// way that names the process — which Windows' power-request state cannot do without
/// administrator rights. Documented Core Audio, called through the vtable as the
/// DirectComposition interop is, with each slot counted out:
///
/// <list type="table">
/// <item><term>IMMDeviceEnumerator</term><description>IUnknown (0–2), EnumAudioEndpoints 3,
/// GetDefaultAudioEndpoint 4</description></item>
/// <item><term>IMMDevice</term><description>IUnknown (0–2), Activate 3</description></item>
/// <item><term>IAudioSessionManager2</term><description>IUnknown (0–2), GetAudioSessionControl
/// 3, GetSimpleAudioVolume 4, GetSessionEnumerator 5</description></item>
/// <item><term>IAudioSessionEnumerator</term><description>IUnknown (0–2), GetCount 3,
/// GetSession 4</description></item>
/// <item><term>IAudioSessionControl2</term><description>IUnknown (0–2), GetState 3, then
/// display name, icon path, grouping param and notification methods (4–11),
/// GetSessionIdentifier 12, GetSessionInstanceIdentifier 13, GetProcessId 14</description></item>
/// </list>
///
/// Read only when a full-screen window is in front, and never throws: no audio device is an
/// empty answer.
/// </remarks>
internal static unsafe class AudioSessions
{
    private static readonly Guid ClsidEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IidManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static readonly Guid IidControl2 = new("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");

    private const int Render = 0;
    private const int Multimedia = 1;
    private const uint ClsctxAll = 0x17;
    private const int Active = 1;

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, uint context, in Guid iid, out IntPtr instance);

    /// <summary>The ids of processes whose audio stream is active right now.</summary>
    public static HashSet<uint> PlayingProcesses()
    {
        var playing = new HashSet<uint>();
        IntPtr enumerator = IntPtr.Zero, device = IntPtr.Zero, manager = IntPtr.Zero, sessions = IntPtr.Zero;
        try
        {
            if (CoCreateInstance(ClsidEnumerator, IntPtr.Zero, ClsctxAll, IidEnumerator, out enumerator) < 0)
            {
                return playing;
            }

            var getDefault = (delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)Slot(enumerator, 4);
            if (getDefault(enumerator, Render, Multimedia, &device) < 0 || device == IntPtr.Zero)
            {
                return playing;
            }

            Guid managerIid = IidManager2;
            var activate = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint, IntPtr, IntPtr*, int>)Slot(device, 3);
            if (activate(device, &managerIid, ClsctxAll, IntPtr.Zero, &manager) < 0 || manager == IntPtr.Zero)
            {
                return playing;
            }

            var getEnumerator = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(manager, 5);
            if (getEnumerator(manager, &sessions) < 0 || sessions == IntPtr.Zero)
            {
                return playing;
            }

            int count;
            var getCount = (delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Slot(sessions, 3);
            if (getCount(sessions, &count) < 0)
            {
                return playing;
            }

            var getSession = (delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr*, int>)Slot(sessions, 4);
            for (int index = 0; index < count; index++)
            {
                IntPtr control = IntPtr.Zero, control2 = IntPtr.Zero;
                try
                {
                    if (getSession(sessions, index, &control) < 0 || control == IntPtr.Zero)
                    {
                        continue;
                    }

                    Guid control2Iid = IidControl2;
                    if (Marshal.QueryInterface(control, in control2Iid, out control2) < 0)
                    {
                        continue;
                    }

                    int state;
                    var getState = (delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Slot(control2, 3);
                    uint process;
                    var getProcess = (delegate* unmanaged[Stdcall]<IntPtr, uint*, int>)Slot(control2, 14);
                    if (getState(control2, &state) >= 0 && state == Active && getProcess(control2, &process) >= 0 && process != 0)
                    {
                        playing.Add(process);
                    }
                }
                finally
                {
                    Release(control2);
                    Release(control);
                }
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            // An answer, not a failure: nothing is known to be playing.
        }
        finally
        {
            Release(sessions);
            Release(manager);
            Release(device);
            Release(enumerator);
        }

        return playing;
    }

    private static IntPtr Slot(IntPtr instance, int index) => (*(IntPtr**)instance)[index];

    private static void Release(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.Release(pointer);
        }
    }
}
