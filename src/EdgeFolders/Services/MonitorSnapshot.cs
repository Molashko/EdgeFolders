namespace EdgeFolders.Services;

public readonly record struct MonitorSnapshot(
    string DeviceName,
    int Left,
    int Top,
    int Right,
    int Bottom,
    int WorkLeft,
    int WorkTop,
    int WorkRight,
    int WorkBottom,
    double DpiScaleX,
    double DpiScaleY)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public int WorkWidth => WorkRight - WorkLeft;
    public int WorkHeight => WorkBottom - WorkTop;
}
