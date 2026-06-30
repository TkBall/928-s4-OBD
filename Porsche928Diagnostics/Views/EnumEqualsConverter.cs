using System.Globalization;
using System.Windows.Data;

namespace Porsche928Diagnostics.Views;

/// <summary>Returns true when the bound enum value equals the converter parameter — used to highlight the active Light/Dark/Auto theme toggle.</summary>
public class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
