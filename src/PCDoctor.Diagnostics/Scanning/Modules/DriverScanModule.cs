using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Scanning.Modules;

internal sealed class DriverScanModule : ScanModuleBase
{
    private readonly IHardwareService _hardware;

    public DriverScanModule(IHardwareService hardware, ILocalizationService localization) : base(localization)
    {
        _hardware = hardware;
    }

    public override ScanModuleKind Kind => ScanModuleKind.Drivers;
    public override string DisplayName => Loc["Module.Drivers"];

    protected override async Task<List<ScanFinding>> CollectAsync(CancellationToken cancellationToken)
    {
        var result = await _hardware.GetProblemDevicesAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Plug and Play device status is unavailable.");
        }

        if (result.Value.Count == 0)
        {
            return
            [
                Finding(
                    Severity.Ok,
                    Loc["Scan.Driver.OkTitle"],
                    Loc["Scan.Driver.OkSummary"],
                    Loc["Scan.Driver.OkEvidence"],
                    "Win32_PnPEntity")
            ];
        }

        return result.Value.Select(device =>
        {
            var severity = device.ConfigManagerErrorCode is 22 or 45 ? Severity.Warning : Severity.Critical;
            return Finding(
                severity,
                device.Name,
                device.ErrorDescription,
                $"DeviceID {device.DeviceId}; class {device.PnpClass}; ConfigManagerErrorCode {device.ConfigManagerErrorCode}; status {device.Status}.",
                "Win32_PnPEntity.ConfigManagerErrorCode");
        }).ToList();
    }
}
