using System.Diagnostics;
using System.Reflection;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;

namespace PCDoctor.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IPrivilegeService _privileges;
    private readonly ILocalizationService _loc;
    private bool _isDarkTheme;
    private LanguageOption _selectedLanguage;

    public SettingsViewModel(
        IThemeService theme,
        IPrivilegeService privileges,
        IAppLogger logger,
        ILocalizationService localization)
    {
        _theme = theme;
        _privileges = privileges;
        _loc = localization;
        _isDarkTheme = theme.CurrentTheme == AppTheme.Dark;
        Languages =
        [
            new LanguageOption(AppLanguage.Turkish, "Türkçe"),
            new LanguageOption(AppLanguage.English, "English")
        ];
        _selectedLanguage = Languages.First(option => option.Language == localization.CurrentLanguage);
        LogDirectory = logger.LogDirectory;
        CurrentLogFile = logger.CurrentLogFile;
        OpenLogsCommand = new RelayCommand(OpenLogs);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        _loc.LanguageChanged += (_, _) => NotifyLocalized();
    }

    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public IReadOnlyList<LanguageOption> Languages { get; }
    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.4.0";
    public string UserName => _privileges.CurrentUser;
    public string Elevation => _privileges.IsAdministrator ? _loc["Settings.Administrator"] : _loc["Settings.StandardUser"];
    public string LogDirectory { get; }
    public string CurrentLogFile { get; }
    public string SafetyNote => _loc["Settings.SafetyNote"];

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value is not null)
            {
                _loc.Apply(value.Language);
            }
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                _theme.Apply(value ? AppTheme.Dark : AppTheme.Light);
            }
        }
    }

    private void NotifyLocalized()
    {
        OnPropertyChanged(nameof(Elevation));
        OnPropertyChanged(nameof(SafetyNote));
    }

    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;

    private void OpenLogs()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = LogDirectory,
            UseShellExecute = true
        });
    }
}
