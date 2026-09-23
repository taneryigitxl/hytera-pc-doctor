using System.Windows;
using System.Windows.Threading;
using PCDoctor.App.ViewModels;
using PCDoctor.App.Views;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;

namespace PCDoctor.App.Services;

public sealed class OverlayController : IOverlayController
{
    private readonly ISettingsStore _settings;
    private readonly IOverlayMetricsService _metrics;
    private readonly ILocalizationService _localization;
    private readonly IAppLogger _logger;
    private readonly OverlayViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private OverlayWindow? _window;
    private bool _busy;

    public OverlayController(
        ISettingsStore settings,
        IOverlayMetricsService metrics,
        ILocalizationService localization,
        IAppLogger logger)
    {
        _settings = settings;
        _metrics = metrics;
        _localization = localization;
        _logger = logger;
        _viewModel = new OverlayViewModel(localization, settings);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        localization.LanguageChanged += (_, _) => _ = RefreshAsync();
    }

    public void Attach(object ownerWindow)
    {
    }

    public void Apply()
    {
        var options = _settings.Overlay;
        if (!options.Enabled)
        {
            _timer.Stop();
            _window?.Hide();
            return;
        }

        _window ??= new OverlayWindow { DataContext = _viewModel, ShowInTaskbar = false };
        _window.Corner = options.Corner;
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.Place();
        if (!_timer.IsEnabled)
        {
            _timer.Start();
            _ = RefreshAsync();
        }
        else
        {
            _window.Place();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy || _window is null || !_settings.Overlay.Enabled)
        {
            return;
        }

        _busy = true;
        try
        {
            var snapshot = await _metrics.SampleAsync().ConfigureAwait(true);
            _viewModel.Apply(snapshot);
            _window.Corner = _settings.Overlay.Corner;
            _window.Place();
        }
        catch (Exception ex)
        {
            _logger.Warn("Overlay sample failed.", ex);
        }
        finally
        {
            _busy = false;
        }
    }
}
