using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Porsche928Diagnostics.Views;

/// <summary>
/// True = signal present / pressure OK → Accent.Text. False = absent → Off
/// (a readable mid-grey, not a near-invisible dim tone — WCAG 2.1 AA fix).
/// </summary>
public class PresenceIsGoodConverter : IValueConverter
{
    public static readonly PresenceIsGoodConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Application.Current.TryFindResource(value is true ? "Accent.Text" : "Off") as Brush
           ?? Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
