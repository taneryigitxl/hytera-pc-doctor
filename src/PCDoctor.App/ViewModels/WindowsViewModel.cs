using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.System;

namespace PCDoctor.App.ViewModels;

public sealed class WindowsViewModel : LoadableViewModel
{
    private readonly ISystemInfoService _systemInfo;
    private OperatingSystemInfo? _lastOs;

    public WindowsViewModel(ISystemInfoService systemInfo, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _systemInfo = systemInfo;
    }

    public ObservableCollection<DetailRow> Rows { get; } = [];
    public ObservableCollection<string> PendingRebootReasons { get; } = [];

    protected override void OnLanguageChanged()
    {
        if (_lastOs is not null)
        {
            Apply(_lastOs);
        }
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _systemInfo.GetOperatingSystemAsync().ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["Windows.Unavailable"]);
        }

        Apply(result.Value);
    }

    private void Apply(OperatingSystemInfo os)
    {
        _lastOs = os;
        var unknown = Loc["Common.Unknown"];
        Rows.Clear();
        Rows.Add(new DetailRow(Loc["Windows.DeviceName"], os.DeviceName, unknown));
        Rows.Add(new DetailRow(Loc["Windows.Edition"], os.Caption, unknown));
        Rows.Add(new DetailRow(Loc["Windows.EditionId"], os.Edition, unknown));
        Rows.Add(new DetailRow(Loc["Windows.DisplayVersion"], os.DisplayVersion, unknown));
        Rows.Add(new DetailRow(Loc["Windows.Version"], os.Version, unknown));
        Rows.Add(new DetailRow(Loc["Windows.Build"], os.Build, unknown));
        Rows.Add(new DetailRow(Loc["Windows.Architecture"], os.Architecture, unknown));
        Rows.Add(new DetailRow(Loc["Windows.InstallDate"], os.InstallDate?.ToString("yyyy-MM-dd HH:mm") ?? unknown, unknown));
        Rows.Add(new DetailRow(Loc["Windows.LastBoot"], os.LastBootTime?.ToString("yyyy-MM-dd HH:mm") ?? unknown, unknown));
        Rows.Add(new DetailRow(Loc["Windows.Uptime"], $"{(int)os.Uptime.TotalDays}d {os.Uptime.Hours}h {os.Uptime.Minutes}m", unknown));
        Rows.Add(new DetailRow(Loc["Windows.Owner"], os.RegisteredOwner, unknown));
        Rows.Add(new DetailRow(Loc["Windows.ProductId"], os.ProductId, unknown));
        Rows.Add(new DetailRow(Loc["Windows.PendingReboot"], os.PendingReboot ? Loc["Common.Yes"] : Loc["Common.No"], unknown));

        PendingRebootReasons.Clear();
        foreach (var reason in os.PendingRebootReasons)
        {
            PendingRebootReasons.Add(reason);
        }
    }
}
