using System.Runtime.InteropServices;

// Minimal interop for the (undocumented but stable) IPolicyConfig COM interface,
// used only to set the default audio endpoint. Enumeration, names and the
// current-default lookup are all done via NAudio (see AudioDevices); this file
// covers the single operation NAudio does not expose: SetDefaultEndpoint.
//
// The CLSID/IID and the vtable layout below are the Vista-era ones that
// CPolicyConfigClient (AudioSes.dll) answers QI for on Win10/11. Setting the
// device for all three roles (Console/Multimedia/Communications) makes the
// choice stick everywhere, matching what popular audio switchers do.

namespace GHelper.Helpers
{
    internal enum PolicyConfigDataFlow
    {
        eRender = 0,   // speakers / headphones
        eCapture = 1,  // microphones
        eAll = 2,
    }

    internal enum PolicyConfigRole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2,
    }

    [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        uint GetMixFormat();
        uint GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bModified, ref IntPtr pFormat);
        uint SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr pEndpointFormat, IntPtr pMixFormat);
        uint GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bDefault, ref long pDefaultPeriod, ref long pMinimumPeriod);
        uint SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref long pPeriod);
        uint GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref uint pMode);
        uint SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref uint pMode);
        uint GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref PolicyConfigPropertyKey key, IntPtr pv);
        uint SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref PolicyConfigPropertyKey key, IntPtr pv);
        [PreserveSig] uint SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PolicyConfigRole role);
        uint SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int bVisible);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PolicyConfigPropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    internal static class PolicyConfig
    {
        private const uint CLSCTX_ALL = 0x17;

        // CPolicyConfigClient coclass (AudioSes.dll) / Vista-era IPolicyConfig IID.
        private static readonly Guid CLSID_PolicyConfigClient = new("294935CE-F637-4E7C-A41B-AB255460B862");

        // Set the given endpoint as the default for every role, so the choice
        // applies to console, multimedia and communications at once.
        public static void SetDefaultEndpoint(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return;

            Guid iid = typeof(IPolicyConfig).GUID;
            Guid clsid = CLSID_PolicyConfigClient;
            int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iid, out IntPtr ppv);
            if (hr != 0)
                throw new System.ComponentModel.Win32Exception(hr,
                    "CoCreateInstance(PolicyConfig) failed: 0x" + hr.ToString("X"));

            IPolicyConfig cfg;
            try { cfg = (IPolicyConfig)Marshal.GetObjectForIUnknown(ppv); }
            finally { Marshal.Release(ppv); }

            cfg.SetDefaultEndpoint(deviceId, PolicyConfigRole.eConsole);
            cfg.SetDefaultEndpoint(deviceId, PolicyConfigRole.eMultimedia);
            cfg.SetDefaultEndpoint(deviceId, PolicyConfigRole.eCommunications);
        }

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(
            ref Guid clsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid iid, out IntPtr ppv);
    }
}
