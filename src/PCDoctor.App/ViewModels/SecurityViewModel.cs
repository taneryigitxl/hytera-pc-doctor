using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Security;

namespace PCDoctor.App.ViewModels;

public sealed class SecurityViewModel : LoadableViewModel
{
    private readonly ISecurityService _security;
    private DefenderStatus _defender = new();

    public SecurityViewModel(ISecurityService security, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _security = security;
    }

    public DefenderStatus Defender
    {
        get => _defender;
        private set
        {
            if (SetProperty(ref _defender, value))
            {
                OnPropertyChanged(nameof(SignatureUpdatedText));
            }
        }
    }

    public string SignatureUpdatedText =>
        Defender.AntivirusSignatureLastUpdated?.ToString("yyyy-MM-dd HH:mm") ?? Loc["Common.Unknown"];

    public ObservableCollection<FirewallProfileStatus> FirewallProfiles { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(SignatureUpdatedText));
        var profiles = FirewallProfiles.ToList();
        FirewallProfiles.Clear();
        foreach (var profile in profiles)
        {
            FirewallProfiles.Add(profile);
        }
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _security.GetSecuritySnapshotAsync().ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["Security.Unavailable"]);
        }

        Defender = result.Value.Defender;
        FirewallProfiles.Clear();
        foreach (var profile in result.Value.FirewallProfiles)
        {
            FirewallProfiles.Add(profile);
        }

        Warnings.Clear();
        foreach (var warning in result.Value.Warnings)
        {
            Warnings.Add(warning);
        }
    }
}
