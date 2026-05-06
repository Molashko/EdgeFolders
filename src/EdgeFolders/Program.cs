using EdgeFolders.Services;

namespace EdgeFolders;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        DpiAwarenessService.TryEnablePerMonitorV2();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
