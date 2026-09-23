namespace PCDoctor.Core.Models.Troubleshooting;

public sealed class TroubleshootScenario
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string PlannedChecks { get; init; } = string.Empty;
    public bool IsImplemented { get; init; }
}
