using PCDoctor.Core.Enums;
using PCDoctor.Core.Formatting;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class StorageScanModule : ScanModuleBase
{
    private readonly IHardwareService _hardware;

    public StorageScanModule(IHardwareService hardware, ILocalizationService localization) : base(localization)
    {
        _hardware = hardware;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Storage;
    public override string DisplayName => Loc["Module.Storage"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _hardware.GetInventoryAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Storage data is unavailable.");
        }

        var findings = new List<ScanFinding>();
        var volumes = result.Value.Volumes.Where(volume => volume.DriveType == "Fixed").ToList();
        if (volumes.Count == 0)
        {
            findings.Add(Finding(Severity.Warning, Loc["Scan.Storage.NoFixed"], Loc["Scan.Storage.NoFixedSummary"], Loc["Scan.Storage.NoFixedEvidence"], "DriveInfo"));
            return findings;
        }

        foreach (var volume in volumes)
        {
            var evidence = Loc.Get(
                "Scan.Storage.Evidence",
                volume.Name,
                volume.FileSystem,
                ByteFormatter.FromBytes(volume.UsedBytes),
                ByteFormatter.FromBytes(volume.TotalBytes),
                ByteFormatter.Percentage(volume.UsagePercent),
                ByteFormatter.FromBytes(volume.FreeBytes));

            if (!volume.IsReady)
            {
                findings.Add(Finding(Severity.Warning, Loc.Get("Scan.Storage.NotReady", volume.Name), Loc["Scan.Storage.NotReadySummary"], evidence, "DriveInfo.IsReady"));
                continue;
            }

            if (volume.UsagePercent >= 95 || volume.FreeBytes < 2L * 1024 * 1024 * 1024)
            {
                findings.Add(Finding(Severity.Critical, Loc.Get("Scan.Storage.CriticalTitle", volume.Name), Loc["Scan.Storage.CriticalSummary"], evidence, "DriveInfo"));
            }
            else if (volume.UsagePercent >= 88 || volume.FreeBytes < 10L * 1024 * 1024 * 1024)
            {
                findings.Add(Finding(Severity.Warning, Loc.Get("Scan.Storage.WarningTitle", volume.Name), Loc["Scan.Storage.WarningSummary"], evidence, "DriveInfo"));
            }
            else
            {
                findings.Add(Finding(Severity.Ok, Loc.Get("Scan.Storage.OkTitle", volume.Name), Loc["Scan.Storage.OkSummary"], evidence, "DriveInfo"));
            }
        }

        foreach (var disk in result.Value.Disks.Where(disk => !string.Equals(disk.Status, "OK", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Finding(
                Severity.Warning,
                Loc.Get("Scan.Storage.DiskStatus", disk.Model),
                Loc["Scan.Storage.DiskStatusSummary"],
                Loc.Get("Scan.Storage.DiskEvidence", disk.DeviceId, disk.Status, ByteFormatter.FromBytes(disk.SizeBytes)),
                "Win32_DiskDrive.Status"));
        }

        return findings;
    }
}
