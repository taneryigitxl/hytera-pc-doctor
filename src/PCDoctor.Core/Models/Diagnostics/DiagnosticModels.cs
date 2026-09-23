using PCDoctor.Core.Enums;

namespace PCDoctor.Core.Models.Diagnostics;

public sealed class ScanFinding
{
    public ScanModuleKind Module { get; init; }
    public Severity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

public sealed class ModuleScanResult
{
    public ScanModuleKind Module { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public Severity HighestSeverity { get; init; }
    public bool Succeeded { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<ScanFinding> Findings { get; init; } = [];
    public TimeSpan Duration { get; init; }
}

public sealed class ScanProgress
{
    public string CurrentModule { get; init; } = string.Empty;
    public int CompletedModules { get; init; }
    public int TotalModules { get; init; }
    public int Percent { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class DiagnosticReport
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string MachineName { get; init; } = Environment.MachineName;
    public string WindowsVersion { get; init; } = "Unknown";
    public IList<ModuleScanResult> Modules { get; init; } = [];
    public bool Cancelled { get; set; }
    public string? ErrorMessage { get; set; }

    public IEnumerable<ScanFinding> AllFindings => Modules.SelectMany(module => module.Findings);

    public int OkCount => AllFindings.Count(finding => finding.Severity == Severity.Ok);
    public int WarningCount => AllFindings.Count(finding => finding.Severity == Severity.Warning);
    public int CriticalCount => AllFindings.Count(finding => finding.Severity == Severity.Critical);
}

public sealed class ReportExportBlueprint
{
    public string SuggestedFileName { get; init; } = "pc-doctor-report";
    public IReadOnlyList<string> SupportedFormats { get; init; } = ["html", "pdf"];
    public string Notes { get; init; } = "HTML and PDF exporters will consume DiagnosticReport without changing this model.";
}

public sealed class RepairActionResult
{
    public string Title { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class RepairReport
{
    public IReadOnlyList<RepairActionResult> Actions { get; init; } = [];
    public int SucceededCount => Actions.Count(action => action.Succeeded);
    public int FailedCount => Actions.Count(action => !action.Succeeded);
}
