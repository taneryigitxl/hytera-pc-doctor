using System.Collections.ObjectModel;
using System.Windows;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.App.ViewModels;

public sealed class ScanViewModel : ObservableObject
{
    private readonly IScanEngine _scanEngine;
    private readonly IRepairService _repair;
    private readonly IReportStore _reports;
    private readonly IAppLogger _logger;
    private readonly ILocalizationService _loc;
    private bool _isScanning;
    private int _progressPercent;
    private string _currentModule;
    private string _status;
    private string? _errorMessage;
    private DiagnosticReport? _report;

    public ScanViewModel(IScanEngine scanEngine, IRepairService repair, IReportStore reports, IAppLogger logger, ILocalizationService localization)
    {
        _scanEngine = scanEngine;
        _repair = repair;
        _reports = reports;
        _logger = logger;
        _loc = localization;
        _currentModule = localization["Scan.Idle"];
        _status = localization["Scan.Ready"];
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsScanning);
        CancelCommand = new RelayCommand(() => StartCommand.Cancel(), () => IsScanning);
        FixCommand = new AsyncRelayCommand(FixAsync, () => CanFix);
        _loc.LanguageChanged += (_, _) => Relocalize();
    }

    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand FixCommand { get; }
    public ObservableCollection<ModuleScanResult> Modules { get; } = [];
    public ObservableCollection<ScanFinding> Findings { get; } = [];

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                StartCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                FixCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIdle => !IsScanning;

    public int ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string CurrentModule
    {
        get => _currentModule;
        private set
        {
            if (SetProperty(ref _currentModule, value))
            {
                OnPropertyChanged(nameof(CurrentModuleLabel));
            }
        }
    }

    public string CurrentModuleLabel => _loc.Get("Scan.CurrentModule", CurrentModule);

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public DiagnosticReport? Report
    {
        get => _report;
        private set
        {
            if (SetProperty(ref _report, value))
            {
                OnPropertyChanged(nameof(HasReport));
                OnPropertyChanged(nameof(OkCount));
                OnPropertyChanged(nameof(WarningCount));
                OnPropertyChanged(nameof(CriticalCount));
                OnPropertyChanged(nameof(ProblemCount));
                OnPropertyChanged(nameof(CanFix));
                FixCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasReport => Report is not null;
    public int OkCount => Report?.OkCount ?? 0;
    public int WarningCount => Report?.WarningCount ?? 0;
    public int CriticalCount => Report?.CriticalCount ?? 0;
    public int ProblemCount => WarningCount + CriticalCount;
    public bool CanFix => !IsScanning && HasReport && ProblemCount > 0;

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        IsScanning = true;
        ErrorMessage = null;
        Modules.Clear();
        Findings.Clear();
        Report = null;
        ProgressPercent = 0;
        CurrentModule = _scanEngine.Modules[0].DisplayName;
        Status = _loc["Scan.Starting"];

        var progress = new Progress<ScanProgress>(update =>
        {
            ProgressPercent = update.Percent;
            CurrentModule = update.CurrentModule;
            Status = update.Status;
        });

        try
        {
            var report = await _scanEngine.RunFullScanAsync(progress, cancellationToken).ConfigureAwait(true);
            ApplyReport(report);
            Status = report.Cancelled
                ? _loc["Scan.Cancelled"]
                : _loc.Get("Scan.Finished", FormatDuration(report));
        }
        catch (OperationCanceledException)
        {
            Status = _loc["Scan.Cancelled"];
        }
        catch (Exception ex)
        {
            _logger.Error("Full scan failed.", ex);
            ErrorMessage = ex.Message;
            Status = _loc["Scan.Failed"];
        }
        finally
        {
            IsScanning = false;
            if (ProgressPercent < 100 && Status != _loc["Scan.Cancelled"])
            {
                ProgressPercent = 100;
            }
        }
    }

    private void ApplyReport(DiagnosticReport report)
    {
        Report = report;
        _reports.Save(report);
        Modules.Clear();
        Findings.Clear();
        foreach (var module in report.Modules)
        {
            Modules.Add(module);
        }

        foreach (var finding in report.AllFindings.OrderByDescending(item => item.Severity))
        {
            Findings.Add(finding);
        }
    }

    private async Task FixAsync()
    {
        if (!HasReport)
        {
            Status = _loc["Scan.FixNeedScan"];
            return;
        }

        var problems = Findings
            .Where(finding => finding.Severity is Severity.Warning or Severity.Critical)
            .ToList();
        if (problems.Count == 0)
        {
            Status = _loc["Scan.FixNone"];
            return;
        }

        var list = string.Join(Environment.NewLine, problems.Select(finding => "• " + finding.Title));
        var confirm = MessageBox.Show(
            _loc.Get("Scan.FixConfirm", list),
            _loc["App.Name"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            Status = _loc["Scan.FixCancelled"];
            return;
        }

        try
        {
            Status = _loc["Scan.FixWorking"];
            var report = await _repair.RepairAsync(problems).ConfigureAwait(true);
            var lines = string.Join(Environment.NewLine, report.Actions.Select(action =>
                $"{(action.Succeeded ? "✓" : "✕")} {action.Title}: {action.Detail}"));
            Status = _loc.Get("Scan.FixFinished", report.SucceededCount, report.FailedCount);
            MessageBox.Show(lines, _loc["App.Name"], MessageBoxButton.OK,
                report.FailedCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _logger.Error("Repair failed.", ex);
            ErrorMessage = ex.Message;
            Status = _loc["Scan.FixFailed"];
        }
    }

    private void Relocalize()
    {
        if (!IsScanning && Report is null)
        {
            CurrentModule = _loc["Scan.Idle"];
            Status = _loc["Scan.Ready"];
        }

        OnPropertyChanged(nameof(CurrentModuleLabel));

        if (Report is not null)
        {
            ApplyReport(Report);
        }
    }

    private string FormatDuration(DiagnosticReport report)
    {
        if (report.CompletedAt is null)
        {
            return _loc["Scan.UnknownDuration"];
        }

        var duration = report.CompletedAt.Value - report.StartedAt;
        return duration.TotalSeconds < 10 ? $"{duration.TotalMilliseconds:0} ms" : $"{duration.TotalSeconds:0.0}s";
    }
}
