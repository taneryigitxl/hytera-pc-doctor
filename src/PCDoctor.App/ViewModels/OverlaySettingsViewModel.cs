using PCDoctor.App.Mvvm;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Overlay;

namespace PCDoctor.App.ViewModels;

public sealed class OverlaySettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly IOverlayController _overlay;

    public OverlaySettingsViewModel(ISettingsStore settings, IOverlayController overlay)
    {
        _settings = settings;
        _overlay = overlay;
    }

    public bool OverlayEnabled
    {
        get => Options.Enabled;
        set
        {
            if (Options.Enabled == value)
            {
                return;
            }

            Options.Enabled = value;
            Persist();
            OnPropertyChanged();
        }
    }

    public bool OverlayTopLeft
    {
        get => Options.Corner == OverlayCorner.TopLeft;
        set { if (value) SetCorner(OverlayCorner.TopLeft); }
    }

    public bool OverlayTopRight
    {
        get => Options.Corner == OverlayCorner.TopRight;
        set { if (value) SetCorner(OverlayCorner.TopRight); }
    }

    public bool OverlayBottomLeft
    {
        get => Options.Corner == OverlayCorner.BottomLeft;
        set { if (value) SetCorner(OverlayCorner.BottomLeft); }
    }

    public bool OverlayBottomRight
    {
        get => Options.Corner == OverlayCorner.BottomRight;
        set { if (value) SetCorner(OverlayCorner.BottomRight); }
    }

    public bool OverlayCpuUsage
    {
        get => Options.ShowCpuUsage;
        set => SetFlag(value, current => Options.ShowCpuUsage = current, nameof(OverlayCpuUsage));
    }

    public bool OverlayCpuTemp
    {
        get => Options.ShowCpuTemp;
        set => SetFlag(value, current => Options.ShowCpuTemp = current, nameof(OverlayCpuTemp));
    }

    public bool OverlayGpuUsage
    {
        get => Options.ShowGpuUsage;
        set => SetFlag(value, current => Options.ShowGpuUsage = current, nameof(OverlayGpuUsage));
    }

    public bool OverlayGpuTemp
    {
        get => Options.ShowGpuTemp;
        set => SetFlag(value, current => Options.ShowGpuTemp = current, nameof(OverlayGpuTemp));
    }

    public bool OverlayRam
    {
        get => Options.ShowRam;
        set => SetFlag(value, current => Options.ShowRam = current, nameof(OverlayRam));
    }

    public bool OverlayDisk
    {
        get => Options.ShowDisk;
        set => SetFlag(value, current => Options.ShowDisk = current, nameof(OverlayDisk));
    }

    private OverlayOptions Options => _settings.Overlay;

    private void SetCorner(OverlayCorner corner)
    {
        if (Options.Corner == corner)
        {
            return;
        }

        Options.Corner = corner;
        Persist();
        OnPropertyChanged(nameof(OverlayTopLeft));
        OnPropertyChanged(nameof(OverlayTopRight));
        OnPropertyChanged(nameof(OverlayBottomLeft));
        OnPropertyChanged(nameof(OverlayBottomRight));
    }

    private void SetFlag(bool value, Action<bool> assign, string propertyName)
    {
        assign(value);
        Persist();
        OnPropertyChanged(propertyName);
    }

    private void Persist()
    {
        _settings.Save();
        _overlay.Apply();
    }
}
