using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Diagnostics;
using PCDoctor.Core.Models.System;

namespace PCDoctor.Diagnostics.Scanning;

public sealed class ScanEngine : IScanEngine
{
    private readonly IAppLogger _logger;
    private readonly ISystemInfoService _systemInfo;
    private readonly ILocalizationService _loc;

    public ScanEngine(IReadOnlyList<IScanModule> modules, IAppLogger logger, ISystemInfoService systemInfo, ILocalizationService localization)
    {
        Modules = modules;
        _logger = logger;
        _systemInfo = systemInfo;
        _loc = localization;
    }

    public IReadOnlyList<IScanModule> Modules { get; }

    public async Task<DiagnosticReport> RunFullScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken = default)
    {
        var os = await SafeOsAsync(cancellationToken).ConfigureAwait(false);
        var report = new DiagnosticReport
        {
            StartedAt = DateTimeOffset.Now,
            MachineName = os.DeviceName,
            WindowsVersion = Collectors.SystemInfoCollector.FormatWindowsVersion(os)
        };

        var total = Modules.Count;
        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var module = Modules[index];
            progress?.Report(new ScanProgress
            {
                CurrentModule = module.DisplayName,
                CompletedModules = index,
                TotalModules = total,
                Percent = (int)Math.Round(index * 100d / total),
                Status = _loc.Get("Scan.Scanning", module.DisplayName)
            });

            _logger.Info($"Scan module started: {module.DisplayName}");
            var started = DateTimeOffset.Now;
            try
            {
                var result = await module.ScanAsync(cancellationToken).ConfigureAwait(false);
                report.Modules.Add(result);
                _logger.Info($"Scan module finished: {module.DisplayName} in {result.Duration.TotalMilliseconds:0} ms.");
            }
            catch (OperationCanceledException)
            {
                report.Cancelled = true;
                report.ErrorMessage = _loc["Scan.Cancelled"];
                report.CompletedAt = DateTimeOffset.Now;
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Scan module {module.DisplayName} failed.", ex);
                report.Modules.Add(new ModuleScanResult
                {
                    Module = module.Kind,
                    DisplayName = module.DisplayName,
                    Succeeded = false,
                    ErrorMessage = ex.Message,
                    Duration = DateTimeOffset.Now - started,
                    HighestSeverity = Core.Enums.Severity.Critical,
                    Findings =
                    [
                        new ScanFinding
                        {
                            Module = module.Kind,
                            Severity = Core.Enums.Severity.Critical,
                            Title = _loc.Get("ScanEngine.ModuleFailedTitle", module.DisplayName),
                            Summary = _loc["ScanEngine.ModuleFailedSummary"],
                            Evidence = ex.Message,
                            Source = module.GetType().FullName ?? module.DisplayName
                        }
                    ]
                });
            }
        }

        progress?.Report(new ScanProgress
        {
            CurrentModule = _loc["Scan.Complete"],
            CompletedModules = total,
            TotalModules = total,
            Percent = 100,
            Status = _loc["Scan.CompleteStatus"]
        });

        report.CompletedAt = DateTimeOffset.Now;
        return report;
    }

    private async Task<OperatingSystemInfo> SafeOsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _systemInfo.GetOperatingSystemAsync(cancellationToken).ConfigureAwait(false);
            return result.Value ?? new OperatingSystemInfo();
        }
        catch (Exception ex)
        {
            _logger.Warn("Scan header could not load operating system details.", ex);
            return new OperatingSystemInfo();
        }
    }
}
