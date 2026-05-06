using System.Windows;

namespace EdgeFolders.Controls;

public sealed class CenteredWrapPanel : System.Windows.Controls.Panel
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(82d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(80d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MinColumnGapProperty = DependencyProperty.Register(
        nameof(MinColumnGap),
        typeof(double),
        typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty MaxColumnGapProperty = DependencyProperty.Register(
        nameof(MaxColumnGap),
        typeof(double),
        typeof(CenteredWrapPanel),
        new FrameworkPropertyMetadata(20d, FrameworkPropertyMetadataOptions.AffectsArrange));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double MinColumnGap
    {
        get => (double)GetValue(MinColumnGapProperty);
        set => SetValue(MinColumnGapProperty, value);
    }

    public double MaxColumnGap
    {
        get => (double)GetValue(MaxColumnGapProperty);
        set => SetValue(MaxColumnGapProperty, value);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        var itemSize = GetItemSize();
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(itemSize);
        }

        if (InternalChildren.Count == 0)
        {
            return new System.Windows.Size(0, 0);
        }

        var width = double.IsInfinity(availableSize.Width)
            ? Math.Min(InternalChildren.Count, 4) * itemSize.Width
            : availableSize.Width;
        var columns = CalculateColumns(width, InternalChildren.Count, itemSize.Width);
        var rows = (int)Math.Ceiling(InternalChildren.Count / (double)columns);
        return new System.Windows.Size(width, rows * itemSize.Height);
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        var itemSize = GetItemSize();
        if (InternalChildren.Count == 0)
        {
            return finalSize;
        }

        var columns = CalculateColumns(finalSize.Width, InternalChildren.Count, itemSize.Width);
        var index = 0;
        var row = 0;

        while (index < InternalChildren.Count)
        {
            var itemsInRow = Math.Min(columns, InternalChildren.Count - index);
            var gap = CalculateGap(finalSize.Width, itemsInRow, itemSize.Width);
            var rowWidth = itemsInRow * itemSize.Width + Math.Max(0, itemsInRow - 1) * gap;
            var x = Math.Max(0, (finalSize.Width - rowWidth) / 2d);
            var y = row * itemSize.Height;

            for (var column = 0; column < itemsInRow; column++)
            {
                InternalChildren[index].Arrange(new Rect(x, y, itemSize.Width, itemSize.Height));
                x += itemSize.Width + gap;
                index++;
            }

            row++;
        }

        return finalSize;
    }

    private System.Windows.Size GetItemSize()
    {
        return new System.Windows.Size(Math.Max(36, ItemWidth), Math.Max(36, ItemHeight));
    }

    private int CalculateColumns(double width, int itemCount, double itemWidth)
    {
        if (itemCount <= 0)
        {
            return 1;
        }

        if (double.IsInfinity(width) || width <= 0)
        {
            return Math.Max(1, Math.Min(itemCount, 4));
        }

        var minGap = Math.Max(0, MinColumnGap);
        var columns = (int)Math.Floor((width + minGap) / (itemWidth + minGap));
        return Math.Max(1, Math.Min(itemCount, columns));
    }

    private double CalculateGap(double width, int itemsInRow, double itemWidth)
    {
        if (itemsInRow <= 1)
        {
            return 0;
        }

        var free = width - itemsInRow * itemWidth;
        var naturalGap = free / Math.Max(1, itemsInRow - 1);
        return Math.Clamp(naturalGap, Math.Max(0, MinColumnGap), Math.Max(MinColumnGap, MaxColumnGap));
    }
}
