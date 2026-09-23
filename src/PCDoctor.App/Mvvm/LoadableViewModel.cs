using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;

namespace PCDoctor.App.Mvvm;

public abstract class LoadableViewModel : ObservableObject
{
    private readonly IAppLogger _logger;
    private bool _isBusy;
    private bool _hasLoaded;
    private string? _errorMessage;
    private string? _statusMessage;

    protected LoadableViewModel(IAppLogger logger, ILocalizationService localization)
    {
        _logger = logger;
        Loc = localization;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        Loc.LanguageChanged += (_, _) =>
        {
            if (_hasLoaded)
            {
                OnLanguageChanged();
            }
        };
    }

    protected ILocalizationService Loc { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public bool IsBusy
    {
        get => _isBusy;
        protected set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    public bool IsIdle => !IsBusy;

    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string? StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    public virtual async Task ActivateAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        _hasLoaded = true;
    }

    public virtual void Deactivate()
    {
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = Loc["Common.CollectingData"];
        try
        {
            await LoadCoreAsync().ConfigureAwait(true);
            StatusMessage = Loc.Get("Common.LastUpdated", DateTime.Now.ToString("HH:mm:ss"));
        }
        catch (Exception ex)
        {
            _logger.Error($"{GetType().Name} failed to load.", ex);
            ErrorMessage = ex.Message;
            StatusMessage = Loc["Common.CollectionFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected virtual void OnLanguageChanged()
    {
        if (!IsBusy)
        {
            _ = RefreshAsync();
        }
    }

    protected abstract Task LoadCoreAsync();
}
