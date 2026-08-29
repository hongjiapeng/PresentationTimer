using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PresentationTimer.Remote.Networking;

internal sealed record NetworkAddressCandidate
{
    public NetworkAddressCandidate(IPAddress address, string interfaceLabel)
    {
        this.Address = address;
        this.InterfaceLabel = interfaceLabel;
    }

    public IPAddress Address { get; }

    public string InterfaceLabel { get; }
}

/// <summary>Provides eligible operational local IPv4 addresses.</summary>
internal interface INetworkAddressProvider
{
    /// <summary>Gets the current eligible addresses and user-facing adapter labels.</summary>
    /// <returns>The current deterministic candidate list.</returns>
    IReadOnlyList<NetworkAddressCandidate> GetCandidates();
}

internal sealed class NetworkAddressProvider : INetworkAddressProvider
{
    private const int EthernetPriority = 1;
    private const int OtherPhysicalPriority = 2;
    private const int VirtualAdapterPriority = 10;
    private const int WirelessPriority = 0;
    private static readonly string[] VirtualAdapterMarkers =
    [
        "hyper-v",
        "virtual",
        "vethernet",
        "vmware",
        "virtualbox",
        "wsl",
        "vpn",
        "wireguard",
        "tailscale",
        "zerotier",
    ];

    public IReadOnlyList<NetworkAddressCandidate> GetCandidates()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(static networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(static networkInterface => networkInterface.GetIPProperties().UnicastAddresses
                .Where(static address => IsEligible(address.Address))
                .Select(address =>
                {
                    string label = networkInterface.Name;
                    if (string.IsNullOrWhiteSpace(label))
                    {
                        label = networkInterface.Description;
                    }

                    return new
                    {
                        Candidate = new NetworkAddressCandidate(address.Address, label),
                        Priority = GetInterfacePriority(
                            networkInterface.Name,
                            networkInterface.Description,
                            networkInterface.NetworkInterfaceType),
                    };
                }))
            .GroupBy(static entry => entry.Candidate.Address)
            .Select(static group => group
                .OrderBy(static entry => entry.Priority)
                .ThenBy(static entry => entry.Candidate.InterfaceLabel, StringComparer.CurrentCultureIgnoreCase)
                .First())
            .OrderBy(static entry => entry.Priority)
            .ThenBy(static entry => entry.Candidate.InterfaceLabel, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static entry => entry.Candidate.Address.ToString(), StringComparer.Ordinal)
            .Select(static entry => entry.Candidate)
            .ToArray();
    }

    internal static int GetInterfacePriority(
        string? name,
        string? description,
        NetworkInterfaceType interfaceType)
    {
        string identity = string.Concat(name, " ", description);
        if (interfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp ||
            VirtualAdapterMarkers.Any(marker => identity.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return VirtualAdapterPriority;
        }

        return interfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => WirelessPriority,
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.Ethernet3Megabit or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.GigabitEthernet => EthernetPriority,
            _ => OtherPhysicalPriority,
        };
    }

    internal static bool IsEligible(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();
        return bytes[0] != 0 && !(bytes[0] == 169 && bytes[1] == 254) && bytes[0] < 224;
    }
}
