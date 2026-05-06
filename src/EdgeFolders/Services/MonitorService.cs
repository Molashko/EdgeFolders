using System.Runtime.InteropServices;

namespace EdgeFolders.Services;

public static class MonitorService
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int EffectiveDpi = 0;

    public static NativePoint GetCursorPosition()
    {
        return GetCursorPos(out var point) ? point : default;
    }

    public static MonitorSnapshot GetCursorMonitor()
    {
        return GetMonitorFromPoint(GetCursorPosition());
    }

    public static MonitorSnapshot GetMonitorFromPoint(NativePoint point)
    {
        var handle = MonitorFromPoint(point, MonitorDefaultToNearest);
        return GetMonitorFromHandle(handle);
    }

    public static MonitorSnapshot GetMonitorFromHandle(IntPtr handle)
    {
        var info = new MonitorInfoEx
        {
            cbSize = Marshal.SizeOf<MonitorInfoEx>()
        };

        if (handle == IntPtr.Zero || !GetMonitorInfo(handle, ref info))
        {
            return new MonitorSnapshot("DISPLAY", 0, 0, 800, 600, 0, 0, 800, 560, 1, 1);
        }

        var scaleX = 1d;
        var scaleY = 1d;
        try
        {
            if (GetDpiForMonitor(handle, EffectiveDpi, out var dpiX, out var dpiY) == 0)
            {
                scaleX = Math.Max(1, dpiX / 96d);
                scaleY = Math.Max(1, dpiY / 96d);
            }
        }
        catch
        {
            // Windows 7/older compatibility fallback.
        }

        return new MonitorSnapshot(
            info.szDevice,
            info.rcMonitor.Left,
            info.rcMonitor.Top,
            info.rcMonitor.Right,
            info.rcMonitor.Bottom,
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right,
            info.rcWork.Bottom,
            scaleX,
            scaleY);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        private const int DeviceNameSize = 32;

        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DeviceNameSize)]
        public string szDevice;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct NativePoint
{
    public int X;
    public int Y;

    public NativePoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
