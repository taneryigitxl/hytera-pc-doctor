using PCDoctor.Core.Enums;
using PCDoctor.Core.Models.Diagnostics;
using PCDoctor.Core.Models.Overlay;

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
    OverlayOptions Overlay { get; }
    void Save();
}

public interface IOverlayMetricsService
{
    Task<OverlaySnapshot> SampleAsync(CancellationToken cancellationToken = default);
}

public interface IOverlayController
{
    void Attach(object ownerWindow);
    void Apply();
    void Dispose();
}

public interface ILocalizationService
{
    AppLanguage CurrentLanguage { get; }
    event EventHandler<AppLanguage>? LanguageChanged;
    string this[string key] { get; }
    string Get(string key, params object?[] args);
    void Apply(AppLanguage language);
}
