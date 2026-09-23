using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Diagnostics;

namespace PCDoctor.Diagnostics.Reporting;

public sealed class InMemoryReportStore : IReportStore
{
    public DiagnosticReport? Latest { get; private set; }
    public event EventHandler<DiagnosticReport?>? LatestChanged;

    public void Save(DiagnosticReport report)
    {
        Latest = report;
        LatestChanged?.Invoke(this, report);
    }
}
