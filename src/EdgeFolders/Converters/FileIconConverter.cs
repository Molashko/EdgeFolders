using System.Globalization;
using System.Windows.Data;
using EdgeFolders.Services;

namespace EdgeFolders.Converters;

public sealed class FileIconConverter : IValueConverter
{
    private static readonly IconService IconService = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = value as string ?? "";
        return IconService.GetIconImage(path);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
