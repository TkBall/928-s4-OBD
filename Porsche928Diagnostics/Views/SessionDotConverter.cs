using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

/// <summary>True = ECU session active → accent green. False = not connected → dark grey.</summary>
public class SessionDotConverter : IValueConverter
{
    public static readonly SessionDotConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(0, 229, 160))
            : new SolidColorBrush(Color.FromRgb(45, 45, 45));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
