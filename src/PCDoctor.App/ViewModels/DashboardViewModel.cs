using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Formatting;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.System;

namespace PCDoctor.App.ViewModels;

public sealed class DashboardViewModel : LoadableViewModel
{
    private readonly ISystemInfoService _systemInfo;
    private CancellationTokenSource? _liveCts;
    private DashboardSnapshot? _lastSnapshot;
    private IReadOnlyList<string> _lastWarnings = [];
    private string _deviceName;
    private string _windowsVersion;
    private string _uptime;

    public DashboardViewModel(ISystemInfoService systemInfo, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _systemInfo = systemInfo;
        _deviceName = localization["Common.Collecting"];
        _windowsVersion = localization["Common.Collecting"];
        _uptime = localization["Common.Collecting"];
    }

    public string DeviceName
    {
        get => _deviceName;
        private set => SetProperty(ref _deviceName, value);
    }

    public string WindowsVersion
    {
        get => _windowsVersion;
        private set => SetProperty(ref _windowsVersion, value);
    }

    public string Uptime
    {
        get => _uptime;
        private set
        {
            if (SetProperty(ref _uptime, value))
            {
                OnPropertyChanged(nameof(UptimeLabel));
            }
        }
    }

    public string UptimeLabel => Loc.Get("Dashboard.Uptime", Uptime);

    public ObservableCollection<MetricTile> Tiles { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public override async Task ActivateAsync()
    {
        await base.ActivateAsync().ConfigureAwait(true);
        _liveCts?.Cancel();
        _liveCts = new CancellationTokenSource();
        _ = RunLiveLoopAsync(_liveCts.Token);
    }

    public override void Deactivate()
    {
        _liveCts?.Cancel();
        _liveCts = null;
    }

    protected override void OnLanguageChanged()
    {
        if (_lastSnapshot is not null)
        {
            Apply(_lastSnapshot, _lastWarnings);
            StatusMessage = Loc.Get("Common.LastUpdated", DateTime.Now.ToString("HH:mm:ss"));
        }
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _systemInfo.GetDashboardAsync().ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["Dashboard.Unavailable"]);
        }

        Apply(result.Value, result.Warnings);
    }

    private async Task RunLiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(true))
            {
                if (IsBusy)
                {
                    continue;
                }

                var result = await _systemInfo.GetDashboardAsync(cancellationToken).ConfigureAwait(true);
                if (result.Succeeded && result.Value is not null)
                {
                    Apply(result.Value, result.Warnings);
                    StatusMessage = Loc.Get("Common.LiveData", DateTime.Now.ToString("HH:mm:ss"));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            StatusMessage = Loc["Common.LivePaused"];
        }
    }

    private void Apply(DashboardSnapshot snapshot, IReadOnlyList<string> warnings)
    {
        _lastSnapshot = snapshot;
        _lastWarnings = warnings;
        DeviceName = snapshot.DeviceName;
        WindowsVersion = snapshot.WindowsVersion;
        Uptime = FormatUptime(snapshot.Uptime);

        Tiles.Clear();
        Tiles.Add(new MetricTile
        {
            Title = Loc["Dashboard.Cpu"],
            Value = ByteFormatter.Percentage(snapshot.CpuUsagePercent),
            Subtitle = snapshot.CpuModel,
            Progress = snapshot.CpuUsagePercent
        });
        Tiles.Add(new MetricTile
        {
            Title = Loc["Dashboard.Memory"],
            Value = $"{ByteFormatter.Percentage(snapshot.RamUsagePercent)}  {ByteFormatter.FromBytes(snapshot.RamUsedBytes)}",
            Subtitle = Loc.Get(
                "Dashboard.MemorySubtitle",
                ByteFormatter.FromBytes(snapshot.RamUsedBytes),
                ByteFormatter.FromBytes(snapshot.RamAvailableBytes),
                ByteFormatter.FromBytes(snapshot.RamTotalBytes)),
            Progress = snapshot.RamUsagePercent
        });
        Tiles.Add(new MetricTile
        {
            Title = Loc["Dashboard.Storage"],
            Value = ByteFormatter.Percentage(snapshot.DiskUsagePercent),
            Subtitle = Loc.Get(
                "Dashboard.StorageSubtitle",
                snapshot.PrimaryVolumeName,
                ByteFormatter.FromBytes(snapshot.DiskUsedBytes),
                ByteFormatter.FromBytes(snapshot.DiskTotalBytes)),
            Progress = snapshot.DiskUsagePercent
        });
        Tiles.Add(new MetricTile { Title = Loc["Dashboard.Gpu"], Value = snapshot.GpuName, Subtitle = Loc["Dashboard.GpuSource"] });
        Tiles.Add(new MetricTile { Title = Loc["Dashboard.Ip"], Value = snapshot.IpAddress, Subtitle = Loc["Dashboard.IpSubtitle"] });
        Tiles.Add(new MetricTile { Title = Loc["Dashboard.UptimeTitle"], Value = FormatUptime(snapshot.Uptime), Subtitle = Loc["Dashboard.UptimeSubtitle"] });

        Warnings.Clear();
        foreach (var warning in warnings)
        {
            Warnings.Add(warning);
        }
    }

    private static string FormatUptime(TimeSpan value)
        => $"{(int)value.TotalDays}d {value.Hours:00}h {value.Minutes:00}m";
}
