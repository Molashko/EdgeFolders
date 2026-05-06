using System.Windows.Forms;

namespace EdgeFolders.Services;

public sealed class TrayService : IDisposable
{
    private readonly IconService _iconService;
    private readonly Action _showOverlay;
    private readonly Action _openSettings;
    private readonly Action _openConfig;
    private readonly Action _exit;
    private NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _icon;

    public TrayService(
        IconService iconService,
        Action showOverlay,
        Action openSettings,
        Action openConfig,
        Action exit)
    {
        _iconService = iconService;
        _showOverlay = showOverlay;
        _openSettings = openSettings;
        _openConfig = openConfig;
        _exit = exit;
    }

    public void Initialize()
    {
        _icon = _iconService.CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "EdgeFolders",
            Visible = true,
            ContextMenuStrip = CreateMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => _showOverlay();
    }

    private ContextMenuStrip CreateMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Показать панель", null, (_, _) => _showOverlay());
        menu.Items.Add("Настройки", null, (_, _) => _openSettings());
        menu.Items.Add("Открыть конфиг", null, (_, _) => _openConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => _exit());
        return menu;
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _icon?.Dispose();
    }
}
