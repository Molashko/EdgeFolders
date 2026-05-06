using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using EdgeFolders.Models;
using EdgeFolders.Services;

namespace EdgeFolders.Windows;

public partial class OverlayWindow : Window, INotifyPropertyChanged
{
    private const int PointerHorizontalGracePx = 18;
    private const int PointerTopGracePx = 10;
    private const int PointerBottomGracePx = 10;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly ConfigService _configService;
    private readonly LaunchService _launchService;
    private readonly ObservableCollection<FolderGroup> _folders = [];
    private MonitorSnapshot? _currentMonitor;
    private string? _currentScreenName;
    private double _hiddenTop;
    private double _visibleTop;
    private int _nativeLeft;
    private int _nativeWidth;
    private int _nativeHeight;
    private int _nativeHiddenTop;
    private int _nativeVisibleTop;
    private bool _isAnimating;

    public OverlayWindow(ConfigService configService, LaunchService launchService)
    {
        _configService = configService;
        _launchService = launchService;

        InitializeComponent();
        DataContext = this;

        _configService.ConfigChanged += (_, _) => ReloadConfig();
        ReloadConfig();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Action? OpenSettingsRequested { get; init; }

    public ObservableCollection<FolderGroup> Folders => _folders;

    public bool IsPointerInsideWindow
    {
        get
        {
            if (!IsVisible || _isAnimating && Opacity < 0.08)
            {
                return false;
            }

            var point = MonitorService.GetCursorPosition();
            if (TryGetNativeBounds(out var bounds))
            {
                return point.X >= bounds.Left - PointerHorizontalGracePx
                       && point.X <= bounds.Right + PointerHorizontalGracePx
                       && point.Y >= bounds.Top - PointerTopGracePx
                       && point.Y <= bounds.Bottom + PointerBottomGracePx;
            }

            var scale = GetWpfDpiScale();
            var nativeLeft = (int)Math.Round(Left * scale.X);
            var nativeTop = (int)Math.Round(Top * scale.Y);
            var nativeWidth = (int)Math.Round(Width * scale.X);
            var nativeHeight = (int)Math.Round(Height * scale.Y);
            return point.X >= nativeLeft - PointerHorizontalGracePx
                   && point.X <= nativeLeft + nativeWidth + PointerHorizontalGracePx
                   && point.Y >= nativeTop - PointerTopGracePx
                   && point.Y <= nativeTop + nativeHeight + PointerBottomGracePx;
        }
    }

    public void ShowNearCursor()
    {
        ShowForMonitor(MonitorService.GetCursorMonitor());
    }

    public void ShowForMonitor(MonitorSnapshot monitor)
    {
        ConfigureForMonitor(monitor);
        _currentMonitor = monitor;
        var isSameScreen = string.Equals(_currentScreenName, monitor.DeviceName, StringComparison.Ordinal);
        var shouldPlayEntrance = !IsVisible
                                 || !isSameScreen
                                 || Opacity < 0.85
                                 || Math.Abs(Top - _visibleTop) > 3;
        _currentScreenName = monitor.DeviceName;

        Topmost = true;
        if (!IsVisible)
        {
            Top = _visibleTop;
            Opacity = 0;
            Show();
            ApplyNativeBounds(_nativeVisibleTop);
        }

        FadeWindow(1, keepVisibleAfter: true);
        if (shouldPlayEntrance)
        {
            Dispatcher.BeginInvoke(AnimateFolderCards, DispatcherPriority.ContextIdle);
        }
    }

    public void HideAnimated()
    {
        if (!IsVisible)
        {
            return;
        }

        ApplyNativeBounds(_nativeVisibleTop);
        FadeWindow(0, keepVisibleAfter: false);
    }

    private void ConfigureForMonitor(MonitorSnapshot monitor)
    {
        var scaleX = Math.Max(1, monitor.DpiScaleX);
        var scaleY = Math.Max(1, monitor.DpiScaleY);
        var logicalLeft = monitor.Left / scaleX;
        var logicalTop = monitor.Top / scaleY;
        var logicalWidth = monitor.Width / scaleX;
        var logicalHeight = monitor.Height / scaleY;
        var maxFolderHeight = _folders.Count == 0 ? 232 : _folders.Max(folder => folder.Height);
        var panelHeight = Math.Max(Math.Max(_configService.Config.Overlay.PanelHeight, 260), maxFolderHeight);

        Height = Math.Min(panelHeight + 28, logicalHeight - 12);
        _nativeWidth = Math.Max(1, monitor.Width);
        _nativeLeft = monitor.Left;
        _nativeHeight = Math.Max(1, (int)Math.Round(Height * scaleY));
        _nativeVisibleTop = monitor.Top - 2;
        _nativeHiddenTop = monitor.Top - _nativeHeight - 16;

        Width = logicalWidth;
        Left = logicalLeft;
        Top = logicalTop - 2;
        FoldersScroll.MaxWidth = Math.Max(320, Width - 20);

        _visibleTop = Top;
        _hiddenTop = logicalTop - Height - 16;
    }

    private void FadeWindow(double opacity, bool keepVisibleAfter)
    {
        _isAnimating = true;

        var duration = keepVisibleAfter
            ? TimeSpan.FromMilliseconds(Math.Max(_configService.Config.Overlay.AnimationMs, 180))
            : TimeSpan.FromMilliseconds(180);
        IEasingFunction ease = keepVisibleAfter
            ? new QuinticEase { EasingMode = EasingMode.EaseOut }
            : new SineEase { EasingMode = EasingMode.EaseInOut };
        var opacityAnimation = new DoubleAnimation(opacity, duration) { EasingFunction = ease };

        opacityAnimation.Completed += (_, _) =>
        {
            _isAnimating = false;
            if (!keepVisibleAfter)
            {
                _currentScreenName = null;
                _currentMonitor = null;
                Hide();
            }
        };

        BeginAnimation(OpacityProperty, opacityAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void ReloadConfig()
    {
        _folders.Clear();

        foreach (var folder in _configService.Config.Folders)
        {
            _folders.Add(folder);
        }

        OnPropertyChanged(nameof(Folders));
        Dispatcher.BeginInvoke(AnimateFolderCards, DispatcherPriority.ContextIdle);
    }

    private void LaunchItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LaunchItem item })
        {
            _launchService.Launch(item);
            HideAnimated();
        }
    }

    private void DropFiles(object sender, System.Windows.DragEventArgs e)
    {
        var folder = _configService.Config.Folders.FirstOrDefault();
        if (folder is null)
        {
            folder = _configService.CreateFolder("Быстрое");
            _configService.Config.Folders.Add(folder);
        }

        AddDroppedFilesToFolder(folder, e);
    }

    private void FolderCard_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FolderGroup folder })
        {
            AddDroppedFilesToFolder(folder, e);
            PulseFolderCard(sender);
        }
    }

    private void FolderCard_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void AddDroppedFilesToFolder(FolderGroup folder, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        foreach (var file in files.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            folder.Items.Add(_configService.CreateItemFromPath(file));
        }

        _configService.Save();
        e.Handled = true;
    }

    private void FolderCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && IsVisible && Opacity < 0.85)
        {
            var index = element.DataContext is FolderGroup folder ? Math.Max(0, _folders.IndexOf(folder)) : 0;
            AnimateFolderCardIn(element, index);
        }
    }

    private void FolderCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateElementTransform(element, 1.014, 0, 180, new CubicEase { EasingMode = EasingMode.EaseOut });
            AnimateShadow(element, 30, 0.38, 180);
        }
    }

    private void FolderCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateElementTransform(element, 1, 0, 220, new CubicEase { EasingMode = EasingMode.EaseOut });
            AnimateShadow(element, 24, 0.26, 220);
        }
    }

    private void Tile_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateElementTransform(element, 1.035, 0, 140, new CubicEase { EasingMode = EasingMode.EaseOut });
        }
    }

    private void Tile_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateElementTransform(element, 1, 0, 190, new CubicEase { EasingMode = EasingMode.EaseOut });
        }
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var scale = GetTransform<ScaleTransform>(element);
        if (scale is null)
        {
            return;
        }

        var pulse = new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(70))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse, HandoffBehavior.SnapshotAndReplace);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse.Clone(), HandoffBehavior.SnapshotAndReplace);
    }

    private void ResizeRightThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (GetFolderFromResizeThumb(sender) is { } folder)
        {
            folder.Width += e.HorizontalChange;
            UpdateOverlayHeightForFolders();
        }
    }

    private void ResizeBottomThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (GetFolderFromResizeThumb(sender) is { } folder)
        {
            folder.Height += e.VerticalChange;
            UpdateOverlayHeightForFolders();
        }
    }

    private void ResizeCornerThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (GetFolderFromResizeThumb(sender) is { } folder)
        {
            folder.Width += e.HorizontalChange;
            folder.Height += e.VerticalChange;
            UpdateOverlayHeightForFolders();
        }
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        UpdateOverlayHeightForFolders();
        _configService.Save();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
        Topmost = false;
        Opacity = 0;
        _currentScreenName = null;
        _currentMonitor = null;
        Hide();
        OpenSettingsRequested?.Invoke();
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
    }

    private FolderGroup? GetFolderFromResizeThumb(object sender)
    {
        return sender is FrameworkElement { DataContext: FolderGroup folder } ? folder : null;
    }

    private void UpdateOverlayHeightForFolders()
    {
        ConfigureForMonitor(_currentMonitor ?? GetMonitorForOverlayOrCursor());
        if (IsVisible)
        {
            ApplyNativeBounds(_nativeVisibleTop);
        }
    }

    private void WindowRoot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // The timer owns hide timing; this method keeps the XAML event intentional.
    }

    private void AnimateFolderCards()
    {
        if (!IsVisible || FoldersCards is null)
        {
            return;
        }

        FoldersCards.UpdateLayout();
        var cards = FindVisualChildren<FrameworkElement>(FoldersCards)
            .Where(element => element.Name == "FolderCard")
            .ToList();

        for (var index = 0; index < cards.Count; index++)
        {
            AnimateFolderCardIn(cards[index], index);
        }
    }

    private void AnimateFolderCardsOut()
    {
        if (FoldersCards is null)
        {
            return;
        }

        var cards = FindVisualChildren<FrameworkElement>(FoldersCards)
            .Where(element => element.Name == "FolderCard")
            .ToList();

        for (var index = 0; index < cards.Count; index++)
        {
            var element = cards[index];
            var translate = GetTransform<TranslateTransform>(element);
            var scale = GetTransform<ScaleTransform>(element);
            var delay = TimeSpan.FromMilliseconds(index * 16);

            translate?.BeginAnimation(TranslateTransform.YProperty, BuildDoubleAnimation(-22, 150, delay, new CubicEase { EasingMode = EasingMode.EaseIn }));
            scale?.BeginAnimation(ScaleTransform.ScaleXProperty, BuildDoubleAnimation(0.98, 150, delay, new CubicEase { EasingMode = EasingMode.EaseIn }));
            scale?.BeginAnimation(ScaleTransform.ScaleYProperty, BuildDoubleAnimation(0.98, 150, delay, new CubicEase { EasingMode = EasingMode.EaseIn }));
        }
    }

    private void AnimateFolderCardIn(FrameworkElement element, int index)
    {
        var translate = GetTransform<TranslateTransform>(element);
        var scale = GetTransform<ScaleTransform>(element);
        var delay = TimeSpan.FromMilliseconds(index * 42);
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 };

        element.Opacity = 0;
        if (translate is not null)
        {
            translate.Y = -34;
            translate.BeginAnimation(TranslateTransform.YProperty, BuildDoubleAnimation(0, 330, delay, ease));
        }

        if (scale is not null)
        {
            scale.ScaleX = 0.965;
            scale.ScaleY = 0.965;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, BuildDoubleAnimation(1, 330, delay, ease));
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, BuildDoubleAnimation(1, 330, delay, ease));
        }

        element.BeginAnimation(OpacityProperty, BuildDoubleAnimation(1, 210, delay, new CubicEase { EasingMode = EasingMode.EaseOut }));
    }

    private void PulseFolderCard(object sender)
    {
        if (sender is FrameworkElement element)
        {
            AnimateElementTransform(element, 1.035, 6, 120, new CubicEase { EasingMode = EasingMode.EaseOut });
            Dispatcher.BeginInvoke(() =>
            {
                AnimateElementTransform(element, 1, 0, 210, new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 });
            }, DispatcherPriority.Background);
        }
    }

    private static void AnimateElementTransform(FrameworkElement element, double scaleValue, double translateY, int milliseconds, IEasingFunction easing)
    {
        var scale = GetTransform<ScaleTransform>(element);
        var translate = GetTransform<TranslateTransform>(element);

        scale?.BeginAnimation(ScaleTransform.ScaleXProperty, BuildDoubleAnimation(scaleValue, milliseconds, TimeSpan.Zero, easing));
        scale?.BeginAnimation(ScaleTransform.ScaleYProperty, BuildDoubleAnimation(scaleValue, milliseconds, TimeSpan.Zero, easing));
        translate?.BeginAnimation(TranslateTransform.YProperty, BuildDoubleAnimation(translateY, milliseconds, TimeSpan.Zero, easing));
    }

    private static void AnimateShadow(FrameworkElement element, double blurRadius, double opacity, int milliseconds)
    {
        if (element.Effect is not DropShadowEffect effect)
        {
            return;
        }

        if (effect.IsFrozen)
        {
            effect = effect.Clone();
            element.Effect = effect;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, BuildDoubleAnimation(blurRadius, milliseconds, TimeSpan.Zero, ease));
        effect.BeginAnimation(DropShadowEffect.OpacityProperty, BuildDoubleAnimation(opacity, milliseconds, TimeSpan.Zero, ease));
    }

    private static DoubleAnimation BuildDoubleAnimation(double to, int milliseconds, TimeSpan delay, IEasingFunction easing)
    {
        return new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
        {
            BeginTime = delay,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
    }

    private static T? GetTransform<T>(FrameworkElement element) where T : Transform
    {
        var group = EnsureMutableTransformGroup(element);
        return group.Children.OfType<T>().FirstOrDefault();
    }

    private static TransformGroup EnsureMutableTransformGroup(FrameworkElement element)
    {
        var currentScaleX = 1d;
        var currentScaleY = 1d;
        var currentTranslateX = 0d;
        var currentTranslateY = 0d;

        if (element.RenderTransform is ScaleTransform scaleOnly)
        {
            currentScaleX = scaleOnly.ScaleX;
            currentScaleY = scaleOnly.ScaleY;
        }
        else if (element.RenderTransform is TranslateTransform translateOnly)
        {
            currentTranslateX = translateOnly.X;
            currentTranslateY = translateOnly.Y;
        }
        else if (element.RenderTransform is TransformGroup existingGroup)
        {
            var existingScale = existingGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
            var existingTranslate = existingGroup.Children.OfType<TranslateTransform>().FirstOrDefault();

            if (existingScale is not null)
            {
                currentScaleX = existingScale.ScaleX;
                currentScaleY = existingScale.ScaleY;
            }

            if (existingTranslate is not null)
            {
                currentTranslateX = existingTranslate.X;
                currentTranslateY = existingTranslate.Y;
            }

            var hasFrozenChild = existingGroup.Children.Any(child => child.IsFrozen);
            if (!existingGroup.IsFrozen
                && !hasFrozenChild
                && existingScale is not null
                && existingTranslate is not null)
            {
                return existingGroup;
            }
        }

        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(currentScaleX, currentScaleY));
        group.Children.Add(new TranslateTransform(currentTranslateX, currentTranslateY));
        element.RenderTransform = group;
        return group;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool TryGetNativeBounds(out NativeRect bounds)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero && GetWindowRect(handle, out bounds))
        {
            return true;
        }

        bounds = default;
        return false;
    }

    private void ApplyNativeBounds(int nativeTop)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            handle,
            HwndTopmost,
            _nativeLeft,
            nativeTop,
            Math.Max(1, _nativeWidth),
            Math.Max(1, _nativeHeight),
            SwpNoActivate | SwpShowWindow);
    }

    private MonitorSnapshot GetMonitorForOverlayOrCursor()
    {
        if (TryGetNativeBounds(out var bounds))
        {
            var centerX = bounds.Left + Math.Max(1, bounds.Right - bounds.Left) / 2;
            var centerY = bounds.Top + Math.Max(1, bounds.Bottom - bounds.Top) / 2;
            return MonitorService.GetMonitorFromPoint(new NativePoint(centerX, centerY));
        }

        return MonitorService.GetCursorMonitor();
    }

    private (double X, double Y) GetWpfDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformToDevice;
        if (transform is { M11: > 0, M22: > 0 })
        {
            return (transform.Value.M11, transform.Value.M22);
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        return (Math.Max(1, dpi.DpiScaleX), Math.Max(1, dpi.DpiScaleY));
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

}
