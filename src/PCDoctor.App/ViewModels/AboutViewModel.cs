using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;

namespace PCDoctor.App.ViewModels;

public sealed class AboutViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;

    public AboutViewModel(ILocalizationService localization)
    {
        _loc = localization;
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.4.0";
        OpenLinkCommand = new RelayCommand(OpenLink);
        Links =
        [
            new AboutLink("\uE774", localization["About.Website"], "yigittaner.com", "https://yigittaner.com"),
            new AboutLink("\uE943", localization["About.GitHub"], "github.com/taneryigitxl", "https://github.com/taneryigitxl"),
            new AboutLink("\uE8F1", localization["About.LinkedIn"], "linkedin.com/in/taneryigit", "https://www.linkedin.com/in/taneryigit")
        ];
        localization.LanguageChanged += (_, _) => RefreshTitles();
    }

    public string Version { get; }
    public ObservableCollection<AboutLink> Links { get; }
    public RelayCommand OpenLinkCommand { get; }
    public string Title => _loc["About.Title"];
    public string Subtitle => _loc["About.Subtitle"];
    public string Tagline => _loc["About.Tagline"];
    public string VersionLabel => _loc.Get("About.Version", Version);
    public string OpenLabel => _loc["About.Open"];

    private void RefreshTitles()
    {
        Links[0] = Links[0] with { Title = _loc["About.Website"] };
        Links[1] = Links[1] with { Title = _loc["About.GitHub"] };
        Links[2] = Links[2] with { Title = _loc["About.LinkedIn"] };
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Tagline));
        OnPropertyChanged(nameof(VersionLabel));
        OnPropertyChanged(nameof(OpenLabel));
    }

    private static void OpenLink(object? parameter)
    {
        if (parameter is not AboutLink link)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = link.Url,
            UseShellExecute = true
        });
    }
}

public sealed record AboutLink(string Icon, string Title, string Host, string Url);
