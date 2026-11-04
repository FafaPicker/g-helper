using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GHelper.AutoGameMode;

internal static class GameProcessPath
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static string Read(Process process)
    {
        // Querying the image path needs fewer rights than inspecting loaded modules.
        IntPtr handle = OpenProcess(0x1000, false, process.Id);
        if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            if (!QueryFullProcessImageName(handle, 0, path, ref length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return path.ToString();
        }
        finally { CloseHandle(handle); }
    }
}
