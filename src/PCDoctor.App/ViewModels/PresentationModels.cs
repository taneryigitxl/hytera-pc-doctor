using PCDoctor.App.Mvvm;
using PCDoctor.Core.Enums;

namespace PCDoctor.App.ViewModels;

public sealed class NavigationItem : ObservableObject
{
    private string _title;

    public NavigationItem(AppSection section, string title, string icon)
    {
        Section = section;
        _title = title;
        Icon = icon;
    }

    public AppSection Section { get; }
    public string Icon { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}

public sealed class MetricTile
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public double Progress { get; init; } = -1;
    public bool ShowProgress => Progress >= 0;
}

public sealed class DetailRow
{
    public DetailRow(string label, string value, string unknownFallback = "Unknown")
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? unknownFallback : value;
    }

    public string Label { get; }
    public string Value { get; }
}

public sealed class DetailCardModel
{
    public string Title { get; init; } = string.Empty;
    public string? Caption { get; init; }
    public IReadOnlyList<DetailRow> Rows { get; init; } = [];
}

public sealed class FilterOption
{
    public FilterOption(string key, string name)
    {
        Key = key;
        Name = name;
    }

    public string Key { get; }
    public string Name { get; }

    public override string ToString() => Name;
}

public sealed class LanguageOption
{
    public LanguageOption(AppLanguage language, string name)
    {
        Language = language;
        Name = name;
    }

    public AppLanguage Language { get; }
    public string Name { get; }
}
