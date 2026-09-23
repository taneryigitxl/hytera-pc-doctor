using System.Windows;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;

namespace PCDoctor.App.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ISettingsStore _settings;

    public ThemeService(ISettingsStore settings)
    {
        _settings = settings;
        CurrentTheme = settings.Theme;
    }

    public AppTheme CurrentTheme { get; private set; }
    public event EventHandler<AppTheme>? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Colors.", StringComparison.OrdinalIgnoreCase) == true);

        var source = theme == AppTheme.Light ? "Themes/Colors.Light.xaml" : "Themes/Colors.Dark.xaml";
        var replacement = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        if (existing is null)
        {
            dictionaries.Insert(0, replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }

        CurrentTheme = theme;
        _settings.Theme = theme;
        ThemeChanged?.Invoke(this, theme);
    }
}
