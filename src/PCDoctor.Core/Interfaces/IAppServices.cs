using PCDoctor.Core.Enums;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Core.Interfaces;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    event EventHandler<AppTheme>? ThemeChanged;
    void Apply(AppTheme theme);
}

public interface IPrivilegeService
{
    bool IsAdministrator { get; }
    string CurrentUser { get; }
}

public interface IReportStore
{
    DiagnosticReport? Latest { get; }
    event EventHandler<DiagnosticReport?>? LatestChanged;
    void Save(DiagnosticReport report);
}

public interface ISettingsStore
{
    AppTheme Theme { get; set; }
    AppLanguage Language { get; set; }
}

public interface ILocalizationService
{
    AppLanguage CurrentLanguage { get; }
    event EventHandler<AppLanguage>? LanguageChanged;
    string this[string key] { get; }
    string Get(string key, params object?[] args);
    void Apply(AppLanguage language);
}
