using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EdgeFolders.Services;
using EdgeFolders.Windows;
using MessageBox = System.Windows.MessageBox;

namespace EdgeFolders;

public partial class App : System.Windows.Application
{
    private const string MutexName = "EdgeFolders.SingleInstance.0C5344FE-DF60-4D0C-B3A3-3A39C2E402F6";

    private Mutex? _mutex;
    private ConfigService? _configService;
    private EdgeWatcher? _edgeWatcher;
    private TrayService? _trayService;
    private OverlayWindow? _overlayWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("EdgeFolders уже запущен в трее.", "EdgeFolders", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        RegisterExceptionLogging();

        var iconService = new IconService();
        var launchService = new LaunchService();
        var startupService = new StartupService();

        _configService = new ConfigService();
        _configService.Load();

        _overlayWindow = new OverlayWindow(_configService, launchService)
        {
            OpenSettingsRequested = () =>
            {
                _edgeWatcher?.MarkOverlayHidden();
                ShowSettings();
            }
        };

        _edgeWatcher = new EdgeWatcher(_configService, () => _overlayWindow.IsPointerInsideWindow);
        _edgeWatcher.ShowRequested += monitor => _overlayWindow.ShowForMonitor(monitor);
        _edgeWatcher.HideRequested += () => _overlayWindow.HideAnimated();
        _edgeWatcher.Start();

        _trayService = new TrayService(
            iconService,
            showOverlay: () =>
            {
                _edgeWatcher.MarkOverlayShown();
                _overlayWindow.ShowNearCursor();
            },
            openSettings: ShowSettings,
            openConfig: () => _configService.OpenConfigFolder(),
            exit: Shutdown);

        _trayService.Initialize();

        if (_configService.Config.Folders.Count == 0)
        {
            ShowSettings();
        }
    }

    private void RegisterExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            CrashLogService.Write(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                CrashLogService.Write(exception, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogService.Write(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    private void ShowSettings()
    {
        if (_configService is null)
        {
            return;
        }

        var startupService = new StartupService();
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_configService, startupService);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.Topmost = true;
        _settingsWindow.Activate();
        _settingsWindow.Dispatcher.BeginInvoke(() =>
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Topmost = false;
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _edgeWatcher?.Dispose();
        _trayService?.Dispose();
        _overlayWindow?.Close();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
