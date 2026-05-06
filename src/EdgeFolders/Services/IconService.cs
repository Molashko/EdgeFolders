using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace EdgeFolders.Services;

public sealed class IconService
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;

    private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    public Drawing.Icon CreateTrayIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            var icon = Drawing.Icon.ExtractAssociatedIcon(processPath);
            if (icon is not null)
            {
                using (icon)
                {
                    return (Drawing.Icon)icon.Clone();
                }
            }
        }

        return CreateGeneratedTrayIcon();
    }

    private static Drawing.Icon CreateGeneratedTrayIcon()
    {
        using var bitmap = new Drawing.Bitmap(32, 32);
        using var graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Drawing.Color.Transparent);

        using var background = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 32, 33, 38));
        using var accent = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 255, 202, 95));
        using var accent2 = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 99, 230, 190));
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(150, 255, 255, 255), 1.4f);

        graphics.FillRoundedRectangle(background, new Drawing.RectangleF(2, 5, 28, 22), 7);
        graphics.DrawRoundedRectangle(pen, new Drawing.RectangleF(2.8f, 5.8f, 26.4f, 20.4f), 6);
        graphics.FillRoundedRectangle(accent, new Drawing.RectangleF(7, 11, 9, 10), 3);
        graphics.FillRoundedRectangle(accent2, new Drawing.RectangleF(17, 11, 8, 10), 3);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public ImageSource GetIconImage(string path, bool large = true)
    {
        var cacheKey = $"{path}|{large}";
        return _iconCache.GetOrAdd(cacheKey, _ => LoadIconImage(path, large));
    }

    private static ImageSource LoadIconImage(string path, bool large)
    {
        var flags = ShgfiIcon | (large ? ShgfiLargeIcon : ShgfiSmallIcon);
        var attributes = 0u;

        if (string.IsNullOrWhiteSpace(path) || LooksLikeUri(path) || !File.Exists(path) && !Directory.Exists(path))
        {
            attributes = FileAttributeNormal;
            flags |= ShgfiUseFileAttributes;
            path = "app.exe";
        }

        var info = new ShFileInfo();
        var result = SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return CreateFallbackImage();
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(large ? 48 : 24, large ? 48 : 24));
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static bool LooksLikeUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile;
    }

    private static ImageSource CreateFallbackImage()
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 202, 95)), null, new Rect(0, 0, 48, 48), 12, 12);
            context.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 32, 33, 38)), null, new Rect(11, 13, 26, 22), 6, 6);
        }

        var bitmap = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Drawing.Graphics graphics, Drawing.Brush brush, Drawing.RectangleF bounds, float radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Drawing.Graphics graphics, Drawing.Pen pen, Drawing.RectangleF bounds, float radius)
    {
        using var path = CreateRoundedPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static Drawing.Drawing2D.GraphicsPath CreateRoundedPath(Drawing.RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
