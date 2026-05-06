using System.Globalization;
using System.Windows.Data;

namespace EdgeFolders.Converters;

public sealed class NumericOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var number = value is double d ? d : System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        var offset = parameter is null ? 0 : System.Convert.ToDouble(parameter, CultureInfo.InvariantCulture);
        return Math.Max(0, number + offset);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
