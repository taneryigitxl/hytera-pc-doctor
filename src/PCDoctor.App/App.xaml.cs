using System.Windows;
using System.Windows.Threading;
using PCDoctor.App.Services;
using PCDoctor.App.ViewModels;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Diagnostics.Composition;
using PCDoctor.Diagnostics.Logging;

namespace PCDoctor.App;

public partial class App : Application
{
    private IAppLogger? _logger;
    private LocalizationService? _localization;
    private IOverlayController? _overlay;
    private TrayIconService? _tray;
    private MainWindow? _mainWindow;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var logger = new FileAppLogger();
        var settings = new JsonSettingsStore(logger);
        _localization = new LocalizationService(settings);
        _localization.Apply(settings.Language);

        var runtime = DiagnosticsRuntime.Create(_localization, logger);
        _logger = runtime.Logger;
        _logger.Info("Tondy Pc Doctor starting.");

        var theme = new ThemeService(settings);
        theme.Apply(settings.Theme);
        _overlay = new OverlayController(settings, runtime.OverlayMetrics, _localization, runtime.Logger);
        var overlaySettings = new OverlaySettingsViewModel(settings, _overlay);

        var main = new MainViewModel(
            new DashboardViewModel(runtime.SystemInfo, runtime.Logger, _localization),
            new ScanViewModel(runtime.ScanEngine, runtime.Repair, runtime.Reports, runtime.Logger, _localization),
            new HardwareViewModel(runtime.Hardware, runtime.Logger, _localization),
            new NetworkViewModel(runtime.Network, runtime.Logger, _localization),
            new WindowsViewModel(runtime.SystemInfo, runtime.Logger, _localization),
            new EventLogsViewModel(runtime.EventLogs, runtime.Logger, _localization),
            new StartupViewModel(runtime.Startup, runtime.Logger, _localization),
            new SecurityViewModel(runtime.Security, runtime.Logger, _localization),
            overlaySettings,
            new ReportsViewModel(runtime.Reports, _localization),
            new SettingsViewModel(theme, runtime.Privileges, runtime.Logger, _localization),
            new AboutViewModel(_localization),
            _localization);

        _mainWindow = new MainWindow { DataContext = main };
        _mainWindow.Closing += OnMainWindowClosing;
        _tray = new TrayIconService(_localization, RestoreMainWindow, ExitApplication);
        _mainWindow.Show();
        if (e.Args.FirstOrDefault(argument => argument.StartsWith("--section=", StringComparison.OrdinalIgnoreCase)) is { } sectionArg
            && Enum.TryParse<PCDoctor.Core.Enums.AppSection>(sectionArg["--section=".Length..], true, out var section))
        {
            main.NavigateTo(section);
        }

        _overlay.Apply();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlay?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = false;
        _mainWindow.Hide();
        _overlay?.Apply();
    }

    private void RestoreMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.ShowInTaskbar = true;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _exiting = true;
        _overlay?.Dispose();
        _overlay = null;
        _tray?.Dispose();
        _tray = null;
        _mainWindow?.Close();
        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Unhandled UI exception.", e.Exception);
        var prefix = _localization?["App.Unhandled"] ?? "An unexpected error occurred.";
        MessageBox.Show(
            $"{prefix}{Environment.NewLine}{e.Exception.Message}",
            "Tondy Pc Doctor",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger?.Error("Unhandled domain exception.", ex);
        }
    }
}
