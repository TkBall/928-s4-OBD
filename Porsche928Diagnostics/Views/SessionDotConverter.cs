using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

/// <summary>
/// True = ECU session active → Accent. False = not connected → Border.Default
/// (visible against the surface at ~3:1, rather than the old near-invisible tone).
/// </summary>
public class SessionDotConverter : IValueConverter
{
    public static readonly SessionDotConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Application.Current.TryFindResource(value is true ? "Accent" : "Border.Default") as Brush
           ?? Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
