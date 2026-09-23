using System.Globalization;
using System.Windows;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;

namespace PCDoctor.App.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ISettingsStore _settings;
    private IReadOnlyDictionary<string, string> _table = LocalizationCatalog.For(AppLanguage.Turkish);
    private ResourceDictionary? _applied;

    public LocalizationService(ISettingsStore settings)
    {
        _settings = settings;
        CurrentLanguage = settings.Language;
        _table = LocalizationCatalog.For(CurrentLanguage);
    }

    public AppLanguage CurrentLanguage { get; private set; }
    public event EventHandler<AppLanguage>? LanguageChanged;

    public string this[string key] => Get(key);

    public string Get(string key, params object?[] args)
    {
        if (!_table.TryGetValue(key, out var template))
        {
            template = key;
        }

        return args.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, args);
    }

    public void Apply(AppLanguage language)
    {
        CurrentLanguage = language;
        _table = LocalizationCatalog.For(language);

        var culture = language == AppLanguage.Turkish
            ? CultureInfo.GetCultureInfo("tr-TR")
            : CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var application = Application.Current;
        if (application is not null)
        {
            var dictionary = new ResourceDictionary();
            foreach (var pair in _table)
            {
                dictionary[pair.Key] = pair.Value;
            }

            if (_applied is not null)
            {
                application.Resources.MergedDictionaries.Remove(_applied);
            }

            application.Resources.MergedDictionaries.Add(dictionary);
            _applied = dictionary;
        }

        _settings.Language = language;
        LanguageChanged?.Invoke(this, language);
    }
}
