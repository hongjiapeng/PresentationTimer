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

                    return new NetworkAddressCandidate(address.Address, label);
                }))
            .DistinctBy(static candidate => candidate.Address)
            .OrderBy(static candidate => candidate.InterfaceLabel, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static candidate => candidate.Address.ToString(), StringComparer.Ordinal)
            .ToArray();
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
