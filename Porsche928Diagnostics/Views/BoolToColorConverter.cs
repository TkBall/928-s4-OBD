using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? new SolidColorBrush(Color.FromRgb(255, 100, 100))
                         : new SolidColorBrush(Color.FromRgb(100, 200, 100));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
