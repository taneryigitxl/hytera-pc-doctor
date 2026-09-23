using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Overlay;

namespace PCDoctor.App.ViewModels;

public sealed class OverlayViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;
    private readonly ISettingsStore _settings;

    public OverlayViewModel(ILocalizationService localization, ISettingsStore settings)
    {
        _loc = localization;
        _settings = settings;
    }

    public ObservableCollection<OverlayLine> Lines { get; } = [];

    public void Apply(OverlaySnapshot snapshot)
    {
        var options = _settings.Overlay;
        Lines.Clear();
        AddCombined(options.ShowCpuUsage, options.ShowCpuTemp, _loc["Overlay.Cpu"], snapshot.CpuUsage, snapshot.CpuTempC);
        AddCombined(options.ShowGpuUsage, options.ShowGpuTemp, _loc["Overlay.Gpu"], snapshot.GpuUsage, snapshot.GpuTempC);
        if (options.ShowRam)
        {
            Lines.Add(new OverlayLine { Label = _loc["Overlay.Ram"], Value = FormatRam(snapshot.RamUsage, snapshot.RamUsedGb) });
        }

        if (options.ShowDisk)
        {
            Lines.Add(new OverlayLine { Label = snapshot.DiskName, Value = FormatUsage(snapshot.DiskUsage) });
        }
    }

    private void AddCombined(bool showUsage, bool showTemp, string label, double? usage, double? temp)
    {
        if (!showUsage && !showTemp)
        {
            return;
        }

        var parts = new List<string>();
        if (showUsage)
        {
            parts.Add(FormatUsage(usage));
        }

        if (showTemp)
        {
            parts.Add(FormatTemp(temp));
        }

        Lines.Add(new OverlayLine { Label = label, Value = string.Join("  ", parts) });
    }

    private string FormatUsage(double? value)
        => value is null ? _loc["Overlay.Unavailable"] : $"{value.Value:0}%";

    private string FormatTemp(double? value)
        => value is null || value <= 5 ? _loc["Overlay.Unavailable"] : $"{value.Value:0}°C";

    private string FormatRam(double? percent, double? usedGb)
    {
        var usage = FormatUsage(percent);
        return usedGb is null ? usage : $"{usage}  {usedGb.Value:0.0} GB";
    }
}
