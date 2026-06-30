using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Porsche928Diagnostics.Theme;

/// <summary>
/// Applies and persists the Fluent 2 Light/Dark/Auto theme by swapping the
/// active Theme/*.xaml ResourceDictionary in Application.Resources.
/// "Auto" follows the Windows "Apps use light/dark mode" setting and updates
/// live via SystemEvents.UserPreferenceChanged.
/// </summary>
public static class ThemeService
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Porsche928Diagnostics", "theme.txt");

    public static AppTheme CurrentPreference { get; private set; } = AppTheme.Auto;

    public static event Action? ThemeChanged;

    public static void Initialize()
    {
        CurrentPreference = LoadPreference();
        Apply(CurrentPreference);

        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && CurrentPreference == AppTheme.Auto)
                Apply(AppTheme.Auto);
        };
    }

    public static void SetPreference(AppTheme theme)
    {
        CurrentPreference = theme;
        SavePreference(theme);
        Apply(theme);
    }

    private static void Apply(AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark || (theme == AppTheme.Auto && IsSystemDarkMode());
        var dictUri = new Uri(dark ? "Theme/Dark.xaml" : "Theme/Light.xaml", UriKind.Relative);
        var newDict = new ResourceDictionary { Source = dictUri };

        var app = Application.Current;
        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.StartsWith("Theme/", StringComparison.Ordinal));

        if (existing != null)
            merged[merged.IndexOf(existing)] = newDict;
        else
            merged.Insert(0, newDict);

        ThemeChanged?.Invoke();
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(RegistryValueName) is int value && value == 0;
        }
        catch
        {
            return true;
        }
    }

    private static AppTheme LoadPreference()
    {
        try
        {
            if (File.Exists(SettingsPath) &&
                Enum.TryParse<AppTheme>(File.ReadAllText(SettingsPath).Trim(), out var theme))
                return theme;
        }
        catch
        {
            // Fall through to default.
        }
        return AppTheme.Auto;
    }

    private static void SavePreference(AppTheme theme)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, theme.ToString());
        }
        catch
        {
            // Persistence is best-effort; failing to save shouldn't crash the app.
        }
    }
}
