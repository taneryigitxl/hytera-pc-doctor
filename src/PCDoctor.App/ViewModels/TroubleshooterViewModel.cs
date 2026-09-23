using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Models.Troubleshooting;

namespace PCDoctor.App.ViewModels;

public sealed class TroubleshooterViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;

    public TroubleshooterViewModel(ILocalizationService localization)
    {
        _loc = localization;
        Scenarios = [];
        Rebuild();
        _loc.LanguageChanged += (_, _) =>
        {
            Rebuild();
            OnPropertyChanged(nameof(Notice));
        };
    }

    public ObservableCollection<TroubleshootScenario> Scenarios { get; }
    public string Notice => _loc["Troubleshooter.Notice"];

    private void Rebuild()
    {
        Scenarios.Clear();
        Scenarios.Add(Scenario("pc-slow", "Troubleshooter.Slow"));
        Scenarios.Add(Scenario("internet-slow", "Troubleshooter.Internet"));
        Scenarios.Add(Scenario("wifi-down", "Troubleshooter.Wifi"));
        Scenarios.Add(Scenario("disk-high", "Troubleshooter.Disk"));
        Scenarios.Add(Scenario("startup-slow", "Troubleshooter.Boot"));
        Scenarios.Add(Scenario("random-crash", "Troubleshooter.Crash"));
    }

    private TroubleshootScenario Scenario(string id, string prefix) => new()
    {
        Id = id,
        Title = _loc[$"{prefix}.Title"],
        Description = _loc[$"{prefix}.Description"],
        PlannedChecks = _loc.Get("Troubleshooter.Checks", _loc[$"{prefix}.Checks"])
    };
}
