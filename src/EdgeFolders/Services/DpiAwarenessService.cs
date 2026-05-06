using System.Runtime.InteropServices;

namespace EdgeFolders.Services;

public static class DpiAwarenessService
{
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    public static void TryEnablePerMonitorV2()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2))
            {
                return;
            }
        }
        catch
        {
            // Older Windows builds may not expose the Per-Monitor V2 API.
        }

        try
        {
            SetProcessDPIAware();
        }
        catch
        {
            // If Windows has already chosen DPI awareness, keep running.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();
}
