using System.Windows;

namespace EdgeFolders.Controls;

public sealed class CenteredRowPanel : System.Windows.Controls.Panel
{
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap),
        typeof(double),
        typeof(CenteredRowPanel),
        new FrameworkPropertyMetadata(14d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        var gap = Math.Max(0, Gap);
        var totalWidth = 0d;
        var maxHeight = 0d;

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new System.Windows.Size(double.PositiveInfinity, availableSize.Height));
            totalWidth += child.DesiredSize.Width;
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
        }

        totalWidth += Math.Max(0, InternalChildren.Count - 1) * gap;
        var viewportWidth = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        return new System.Windows.Size(Math.Max(totalWidth, viewportWidth), maxHeight);
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        var gap = Math.Max(0, Gap);
        var totalWidth = InternalChildren
            .OfType<UIElement>()
            .Sum(child => child.DesiredSize.Width)
            + Math.Max(0, InternalChildren.Count - 1) * gap;
        var x = Math.Max(0, (finalSize.Width - totalWidth) / 2d);

        foreach (UIElement child in InternalChildren)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect(x, 0, size.Width, Math.Max(finalSize.Height, size.Height)));
            x += size.Width + gap;
        }

        return finalSize;
    }
}
