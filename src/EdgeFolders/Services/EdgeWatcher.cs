using System.Windows.Threading;

namespace EdgeFolders.Services;

public sealed class EdgeWatcher : IDisposable
{
    private const int MinHideDelayMs = 260;

    private readonly ConfigService _configService;
    private readonly Func<bool> _isPointerInsideOverlay;
    private readonly DispatcherTimer _timer;
    private DateTime? _leftOverlayAt;
    private bool _overlayVisible;
    private string? _visibleScreenName;

    public EdgeWatcher(ConfigService configService, Func<bool> isPointerInsideOverlay)
    {
        _configService = configService;
        _isPointerInsideOverlay = isPointerInsideOverlay;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(32)
        };
        _timer.Tick += OnTick;
    }

    public event Action<MonitorSnapshot>? ShowRequested;
    public event Action? HideRequested;

    public void Start() => _timer.Start();

    public void MarkOverlayShown()
    {
        _overlayVisible = true;
        _leftOverlayAt = null;
    }

    public void MarkOverlayHidden()
    {
        _overlayVisible = false;
        _leftOverlayAt = null;
        _visibleScreenName = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var config = _configService.Config;
        if (!config.EnableEdgeHover)
        {
            return;
        }

        var point = MonitorService.GetCursorPosition();
        var screen = MonitorService.GetMonitorFromPoint(point);
        var inHorizontalBounds = point.X >= screen.Left && point.X < screen.Right;
        var inHotZone = inHorizontalBounds
                        && point.Y >= screen.Top
                        && point.Y <= screen.Top + config.Overlay.EdgeHotZone;

        if (inHotZone)
        {
            _leftOverlayAt = null;
            if (!_overlayVisible || !string.Equals(_visibleScreenName, screen.DeviceName, StringComparison.Ordinal))
            {
                _overlayVisible = true;
                _visibleScreenName = screen.DeviceName;
                ShowRequested?.Invoke(screen);
            }

            return;
        }

        if (!_overlayVisible)
        {
            return;
        }

        if (_isPointerInsideOverlay())
        {
            _leftOverlayAt = null;
            return;
        }

        _leftOverlayAt ??= DateTime.UtcNow;
        var hideDelayMs = Math.Max(config.Overlay.HideDelayMs, MinHideDelayMs);
        if ((DateTime.UtcNow - _leftOverlayAt.Value).TotalMilliseconds >= hideDelayMs)
        {
            _overlayVisible = false;
            _leftOverlayAt = null;
            _visibleScreenName = null;
            HideRequested?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
