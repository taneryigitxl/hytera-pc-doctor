using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.App.ViewModels;

public sealed class ReportsViewModel : ObservableObject
{
    private readonly IReportStore _store;
    private readonly ILocalizationService _loc;
    private DiagnosticReport? _report;

    public ReportsViewModel(IReportStore store, ILocalizationService localization)
    {
        _store = store;
        _loc = localization;
        _report = store.Latest;
        Rebuild();
        _store.LatestChanged += (_, report) =>
        {
            Report = report;
            Rebuild();
        };
        _loc.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(MachineName));
            OnPropertyChanged(nameof(WindowsVersion));
            OnPropertyChanged(nameof(ExportNote));
            OnPropertyChanged(nameof(StartedLabel));
            OnPropertyChanged(nameof(CompletedLabel));
            OnPropertyChanged(nameof(ExportFileName));
            Rebuild();
        };
    }

    public DiagnosticReport? Report
    {
        get => _report;
        private set
        {
            if (SetProperty(ref _report, value))
            {
                OnPropertyChanged(nameof(HasReport));
                OnPropertyChanged(nameof(MachineName));
                OnPropertyChanged(nameof(WindowsVersion));
                OnPropertyChanged(nameof(StartedAt));
                OnPropertyChanged(nameof(CompletedAt));
                OnPropertyChanged(nameof(OkCount));
                OnPropertyChanged(nameof(WarningCount));
                OnPropertyChanged(nameof(CriticalCount));
                OnPropertyChanged(nameof(StartedLabel));
                OnPropertyChanged(nameof(CompletedLabel));
            }
        }
    }

    public bool HasReport => Report is not null;
    public string MachineName => Report?.MachineName ?? _loc["Reports.NoScan"];
    public string WindowsVersion => Report?.WindowsVersion ?? _loc["Reports.RunScan"];
    public string StartedAt => Report?.StartedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    public string CompletedAt => Report?.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    public int OkCount => Report?.OkCount ?? 0;
    public int WarningCount => Report?.WarningCount ?? 0;
    public int CriticalCount => Report?.CriticalCount ?? 0;
    public string StartedLabel => _loc.Get("Reports.Started", StartedAt);
    public string CompletedLabel => _loc.Get("Reports.Completed", CompletedAt);
    public string ExportFileName => _loc.Get("Reports.FileName", ExportBlueprint.SuggestedFileName);
    public string ExportNote => _loc["Reports.ExportNote"];
    public ReportExportBlueprint ExportBlueprint { get; } = new();
    public ObservableCollection<ScanFinding> Findings { get; } = [];

    private void Rebuild()
    {
        Findings.Clear();
        if (Report is null)
        {
            return;
        }

        foreach (var finding in Report.AllFindings)
        {
            Findings.Add(finding);
        }
    }
}
