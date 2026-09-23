using System.Collections.ObjectModel;
using System.Windows;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Startup;

namespace PCDoctor.App.ViewModels;

public sealed class StartupViewModel : LoadableViewModel
{
    private readonly IStartupService _startup;
    private StartupEntry? _selectedEntry;

    public StartupViewModel(IStartupService startup, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _startup = startup;
        EnableCommand = new AsyncRelayCommand(() => ToggleAsync(true), () => CanEnable);
        DisableCommand = new AsyncRelayCommand(() => ToggleAsync(false), () => CanDisable);
    }

    public ObservableCollection<StartupEntry> Entries { get; } = [];
    public AsyncRelayCommand EnableCommand { get; }
    public AsyncRelayCommand DisableCommand { get; }
    public int Count => Entries.Count;

    public StartupEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                EnableCommand.RaiseCanExecuteChanged();
                DisableCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanEnable => SelectedEntry is { CanToggle: true, IsEnabled: false };
    public bool CanDisable => SelectedEntry is { CanToggle: true, IsEnabled: true };

    protected override void OnLanguageChanged()
    {
        StatusMessage = Loc.Get("Startup.Loaded", Entries.Count);
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _startup.GetStartupEntriesAsync().ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["Startup.Unavailable"]);
        }

        Entries.Clear();
        foreach (var entry in result.Value)
        {
            Entries.Add(entry);
        }

        OnPropertyChanged(nameof(Count));
        EnableCommand.RaiseCanExecuteChanged();
        DisableCommand.RaiseCanExecuteChanged();
        StatusMessage = Loc.Get("Startup.Loaded", Entries.Count);
    }

    private async Task ToggleAsync(bool enable)
    {
        if (SelectedEntry is null)
        {
            StatusMessage = Loc["Startup.SelectItem"];
            return;
        }

        if (!SelectedEntry.CanToggle)
        {
            StatusMessage = Loc["Startup.CannotToggle"];
            return;
        }

        if (!enable)
        {
            var confirm = MessageBox.Show(
                Loc.Get("Startup.DisableConfirm", SelectedEntry.Name),
                Loc["App.Name"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var result = await _startup.SetEnabledAsync(SelectedEntry, enable).ConfigureAwait(true);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? Loc["Startup.ActionFailed"];
            StatusMessage = Loc["Startup.ActionFailed"];
            return;
        }

        ErrorMessage = null;
        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = Loc.Get("Startup.Toggled", enable ? Loc["Startup.Enabled"] : Loc["Startup.Disabled"]);
    }
}
