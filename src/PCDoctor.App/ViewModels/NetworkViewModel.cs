using System.Collections.ObjectModel;
using PCDoctor.App.Mvvm;
using PCDoctor.Core.Interfaces;
using PCDoctor.Core.Logging;
using PCDoctor.Core.Models.Network;

namespace PCDoctor.App.ViewModels;

public sealed class NetworkViewModel : LoadableViewModel
{
    private readonly INetworkService _network;
    private bool _internetReachable;

    public NetworkViewModel(INetworkService network, IAppLogger logger, ILocalizationService localization)
        : base(logger, localization)
    {
        _network = network;
    }

    public ObservableCollection<DetailCardModel> Cards { get; } = [];
    public ObservableCollection<ConnectivityTest> Tests { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public bool InternetReachable
    {
        get => _internetReachable;
        private set => SetProperty(ref _internetReachable, value);
    }

    protected override async Task LoadCoreAsync()
    {
        var result = await _network.DiagnoseAsync().ConfigureAwait(true);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? Loc["Network.Unavailable"]);
        }

        Apply(result.Value);
    }

    private void Apply(NetworkDiagnosticsResult data)
    {
        var unknown = Loc["Common.Unknown"];
        InternetReachable = data.InternetReachable;
        Cards.Clear();
        Cards.Add(new DetailCardModel
        {
            Title = Loc["Network.ActiveAdapter"],
            Caption = data.ActiveAdapter.Description,
            Rows =
            [
                new(Loc["Hardware.Name"], data.ActiveAdapter.Name, unknown),
                new(Loc["Hardware.Type"], data.ActiveAdapter.Type, unknown),
                new(Loc["Hardware.Status"], data.ActiveAdapter.Status, unknown),
                new(Loc["Network.LocalIp"], data.ActiveAdapter.LocalIp, unknown),
                new(Loc["Hardware.Gateway"], data.ActiveAdapter.Gateway, unknown),
                new(Loc["Hardware.Dns"], data.ActiveAdapter.Dns, unknown),
                new(Loc["Hardware.Mac"], data.ActiveAdapter.MacAddress, unknown),
                new(Loc["Network.LinkSpeed"], data.ActiveAdapter.LinkSpeed, unknown),
                new(Loc["Network.OsAvailable"], data.IsNetworkAvailable ? Loc["Common.Yes"] : Loc["Common.No"], unknown)
            ]
        });

        Tests.Clear();
        Tests.Add(data.HttpTest);
        Tests.Add(data.DnsTest);
        Tests.Add(data.PingTest);
        Tests.Add(data.GatewayPingTest);

        Warnings.Clear();
        foreach (var warning in data.Warnings)
        {
            Warnings.Add(warning);
        }
    }
}
