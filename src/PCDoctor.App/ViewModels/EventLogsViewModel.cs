using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Events;

namespace PCDoctor.App.ViewModels;

public sealed class EventLogsViewModel : LoadableViewModel
{
    private readonly IEventLogService _eventLogs;
    private IReadOnlyList<WindowsEventRecord> _all = [];
    private string _sourceFilter = string.Empty;
    private string _messageFilter = string.Empty;
    private FilterOption? _selectedLevel;
    private FilterOption? _selectedLog;
    private string? _eventIdText;

    public EventLogsViewModel(IEventLogService eventLogs, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _eventLogs = eventLogs;
        ApplyFilterCommand = new RelayCommand(ApplyFilter);
        RebuildOptions();
    }

    public RelayCommand ApplyFilterCommand { get; }
    public ObservableCollection<WindowsEventRecord> Events { get; } = [];
    public ObservableCollection<FilterOption> Levels { get; } = [];
    public ObservableCollection<FilterOption> Logs { get; } = [];

    public string SourceFilter
    {
        get => _sourceFilter;
        set => SetProperty(ref _sourceFilter, value);
    }

    public string MessageFilter
    {
        get => _messageFilter;
        set => SetProperty(ref _messageFilter, value);
    }

    public FilterOption? SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            if (SetProperty(ref _selectedLevel, value))
            {
                ApplyFilter();
            }
        }
    }

    public FilterOption? SelectedLog
    {
        get => _selectedLog;
        set
        {
            if (SetProperty(ref _selectedLog, value))
            {
                ApplyFilter();
            }
        }
    }

    public string? EventIdText
    {
        get => _eventIdText;
        set => SetProperty(ref _eventIdText, value);
    }

    public int VisibleCount => Events.Count;
    public int TotalCount => _all.Count;
    public string VisibleCountLabel => Loc.Get("EventLogs.Showing", VisibleCount);

    protected override void OnLanguageChanged()
    {
        var levelKey = SelectedLevel?.Key ?? "all";
        var logKey = SelectedLog?.Key ?? "all";
        RebuildOptions();
        SelectedLevel = Levels.First(option => option.Key == levelKey);
        SelectedLog = Logs.First(option => option.Key == logKey);
        ApplyFilter();
        if (_all.Count > 0)
        {
            StatusMessage = Loc.Get("EventLogs.Loaded", _all.Count);
        }
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _eventLogs.QueryAsync(new EventLogQueryOptions()).ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["EventLogs.Unavailable"]);
        }

        _all = result.Value;
        ApplyFilter();
        StatusMessage = Loc.Get("EventLogs.Loaded", _all.Count);
    }

    private void RebuildOptions()
    {
        Levels.Clear();
        Levels.Add(new FilterOption("all", Loc["EventLogs.All"]));
        Levels.Add(new FilterOption("critical", Loc["EventLogs.Critical"]));
        Levels.Add(new FilterOption("error", Loc["EventLogs.Error"]));
        Logs.Clear();
        Logs.Add(new FilterOption("all", Loc["EventLogs.All"]));
        Logs.Add(new FilterOption("System", Loc["EventLogs.System"]));
        Logs.Add(new FilterOption("Application", Loc["EventLogs.Application"]));
        _selectedLevel ??= Levels[0];
        _selectedLog ??= Logs[0];
        OnPropertyChanged(nameof(SelectedLevel));
        OnPropertyChanged(nameof(SelectedLog));
    }

    private void ApplyFilter()
    {
        int? eventId = int.TryParse(EventIdText, out var parsed) ? parsed : null;
        var levelKey = SelectedLevel?.Key ?? "all";
        var logKey = SelectedLog?.Key ?? "all";
        var filtered = _all.Where(record =>
        {
            if (!MatchesLevel(record, levelKey))
            {
                return false;
            }

            if (logKey != "all" && !record.LogName.Equals(logKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SourceFilter) &&
                record.Source.Contains(SourceFilter, StringComparison.OrdinalIgnoreCase) is false)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(MessageFilter) &&
                record.Message.Contains(MessageFilter, StringComparison.OrdinalIgnoreCase) is false)
            {
                return false;
            }

            return eventId is null || record.EventId == eventId;
        });

        Events.Clear();
        foreach (var record in filtered)
        {
            Events.Add(record);
        }

        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(VisibleCountLabel));
    }

    private static bool MatchesLevel(WindowsEventRecord record, string key) => key switch
    {
        "critical" => record.LevelCode == 1 || ContainsAny(record.Level, "Critical", "Kritik"),
        "error" => record.LevelCode == 2 || ContainsAny(record.Level, "Error", "Hata"),
        _ => true
    };

    private static bool ContainsAny(string value, params string[] tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
