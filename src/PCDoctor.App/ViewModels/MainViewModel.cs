using PCDoctor.App.Mvvm;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;

namespace PCDoctor.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;
    private NavigationItem _selectedNavigation;
    private object _currentView = null!;
    private string _statusText;

    public MainViewModel(
        DashboardViewModel dashboard,
        ScanViewModel scan,
        HardwareViewModel hardware,
        NetworkViewModel network,
        WindowsViewModel windows,
        EventLogsViewModel eventLogs,
        StartupViewModel startup,
        SecurityViewModel security,
        OverlaySettingsViewModel overlay,
        ReportsViewModel reports,
        SettingsViewModel settings,
        AboutViewModel about,
        ILocalizationService localization)
    {
        _loc = localization;
        Dashboard = dashboard;
        Scan = scan;
        Hardware = hardware;
        Network = network;
        Windows = windows;
        EventLogs = eventLogs;
        Startup = startup;
        Security = security;
        Overlay = overlay;
        Reports = reports;
        Settings = settings;
        About = about;

        Navigation =
        [
            new NavigationItem(AppSection.Dashboard, localization["Nav.Dashboard"], "\uE80F"),
            new NavigationItem(AppSection.SystemScan, localization["Nav.SystemScan"], "\uE721"),
            new NavigationItem(AppSection.Hardware, localization["Nav.Hardware"], "\uE950"),
            new NavigationItem(AppSection.Network, localization["Nav.Network"], "\uE968"),
            new NavigationItem(AppSection.Windows, localization["Nav.Windows"], "\uE770"),
            new NavigationItem(AppSection.EventLogs, localization["Nav.EventLogs"], "\uE7C3"),
            new NavigationItem(AppSection.Startup, localization["Nav.Startup"], "\uE7E8"),
            new NavigationItem(AppSection.Security, localization["Nav.Security"], "\uE72E"),
            new NavigationItem(AppSection.Overlay, localization["Nav.Overlay"], "\uE9D9"),
            new NavigationItem(AppSection.Reports, localization["Nav.Reports"], "\uE9F9"),
            new NavigationItem(AppSection.Settings, localization["Nav.Settings"], "\uE713"),
            new NavigationItem(AppSection.About, localization["Nav.About"], "\uE946")
        ];

        _statusText = localization["Common.Ready"];
        _selectedNavigation = Navigation[0];
        _currentView = dashboard;
        _loc.LanguageChanged += (_, _) => RefreshNavigation();
        _ = NavigateAsync(Navigation[0]);
    }

    public IReadOnlyList<NavigationItem> Navigation { get; }
    public DashboardViewModel Dashboard { get; }
    public ScanViewModel Scan { get; }
    public HardwareViewModel Hardware { get; }
    public NetworkViewModel Network { get; }
    public WindowsViewModel Windows { get; }
    public EventLogsViewModel EventLogs { get; }
    public StartupViewModel Startup { get; }
    public SecurityViewModel Security { get; }
    public OverlaySettingsViewModel Overlay { get; }
    public ReportsViewModel Reports { get; }
    public SettingsViewModel Settings { get; }
    public AboutViewModel About { get; }

    public NavigationItem SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (SetProperty(ref _selectedNavigation, value) && value is not null)
            {
                _ = NavigateAsync(value);
            }
        }
    }

    public object CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private void RefreshNavigation()
    {
        foreach (var item in Navigation)
        {
            item.Title = _loc[NavKey(item.Section)];
        }

        StatusText = SelectedNavigation.Title;
    }

    private static string NavKey(AppSection section) => section switch
    {
        AppSection.Dashboard => "Nav.Dashboard",
        AppSection.SystemScan => "Nav.SystemScan",
        AppSection.Hardware => "Nav.Hardware",
        AppSection.Network => "Nav.Network",
        AppSection.Windows => "Nav.Windows",
        AppSection.EventLogs => "Nav.EventLogs",
        AppSection.Startup => "Nav.Startup",
        AppSection.Security => "Nav.Security",
        AppSection.Overlay => "Nav.Overlay",
        AppSection.Reports => "Nav.Reports",
        AppSection.Settings => "Nav.Settings",
        AppSection.About => "Nav.About",
        _ => "Nav.Dashboard"
    };

    private async Task NavigateAsync(NavigationItem item)
    {
        DeactivateCurrent();
        var viewModel = Resolve(item.Section);
        CurrentView = viewModel;
        StatusText = item.Title;
        if (viewModel is LoadableViewModel loadable)
        {
            await loadable.ActivateAsync().ConfigureAwait(true);
        }
    }

    private void DeactivateCurrent()
    {
        if (CurrentView is LoadableViewModel loadable)
        {
            loadable.Deactivate();
        }
    }

    private object Resolve(AppSection section) => section switch
    {
        AppSection.Dashboard => Dashboard,
        AppSection.SystemScan => Scan,
        AppSection.Hardware => Hardware,
        AppSection.Network => Network,
        AppSection.Windows => Windows,
        AppSection.EventLogs => EventLogs,
        AppSection.Startup => Startup,
        AppSection.Security => Security,
        AppSection.Overlay => Overlay,
        AppSection.Reports => Reports,
        AppSection.Settings => Settings,
        AppSection.About => About,
        _ => Dashboard
    };

    public void NavigateTo(AppSection section)
    {
        var item = Navigation.FirstOrDefault(entry => entry.Section == section);
        if (item is not null)
        {
            SelectedNavigation = item;
        }
    }
}
