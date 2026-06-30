using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

/// <summary>
/// True = error/alarm state → Alert.Text. False = OK → Accent.Text.
/// Resolves through Application.Current.TryFindResource so the color
/// always reflects whichever Theme/*.xaml is currently merged.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Application.Current.TryFindResource(value is true ? "Alert.Text" : "Accent.Text") as Brush
           ?? Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
