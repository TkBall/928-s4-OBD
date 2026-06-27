using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

/// <summary>True = signal present / pressure OK → accent green. False = absent/fault → dim grey.</summary>
public class PresenceIsGoodConverter : IValueConverter
{
    public static readonly PresenceIsGoodConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(0, 229, 160))
            : new SolidColorBrush(Color.FromRgb(42, 42, 42));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
