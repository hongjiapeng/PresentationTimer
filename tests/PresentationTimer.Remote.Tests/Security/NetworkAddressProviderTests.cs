using System.Collections.Immutable;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.AspNetCore.WebUtilities;
using PresentationTimer.Remote.Networking;
using RemoteHost = PresentationTimer.Remote.RemoteSessionHost;

namespace PresentationTimer.Remote.Tests.Security;

/// <summary>Verifies deterministic LAN address filtering.</summary>
[TestClass]
public sealed class NetworkAddressProviderTests
{
    /// <summary>Verifies only routable unicast IPv4 addresses are eligible.</summary>
    [TestMethod]
    public void IsEligible_WithCommonAddressClasses_FiltersUnsafeCandidates()
    {
        Assert.IsTrue(NetworkAddressProvider.IsEligible(IPAddress.Parse("192.168.1.20")));
        Assert.IsTrue(NetworkAddressProvider.IsEligible(IPAddress.Parse("10.20.30.40")));
        Assert.IsFalse(NetworkAddressProvider.IsEligible(IPAddress.Loopback));
        Assert.IsFalse(NetworkAddressProvider.IsEligible(IPAddress.Parse("169.254.10.20")));
        Assert.IsFalse(NetworkAddressProvider.IsEligible(IPAddress.Parse("224.0.0.1")));
        Assert.IsFalse(NetworkAddressProvider.IsEligible(IPAddress.IPv6Loopback));
    }

    /// <summary>Verifies physical LAN adapters are preferred over virtual and tunnel adapters.</summary>
    [TestMethod]
    public void GetInterfacePriority_PhysicalAndVirtualAdapters_PrefersPhysicalLan()
    {
        // Act
        int wirelessPriority = NetworkAddressProvider.GetInterfacePriority(
            "WLAN",
            "Intel(R) Wi-Fi 6E AX211 160MHz",
            NetworkInterfaceType.Wireless80211);
        int ethernetPriority = NetworkAddressProvider.GetInterfacePriority(
            "Ethernet",
            "Intel(R) Ethernet Controller",
            NetworkInterfaceType.Ethernet);
        int virtualPriority = NetworkAddressProvider.GetInterfacePriority(
            "vEthernet (WSL)",
            "Hyper-V Virtual Ethernet Adapter",
            NetworkInterfaceType.Ethernet);
        int tunnelPriority = NetworkAddressProvider.GetInterfacePriority(
            "VPN",
            "WireGuard Tunnel",
            NetworkInterfaceType.Tunnel);

        // Assert
        Assert.IsLessThan(virtualPriority, wirelessPriority);
        Assert.IsLessThan(virtualPriority, ethernetPriority);
        Assert.AreEqual(virtualPriority, tunnelPriority);
    }

    /// <summary>Verifies endpoint binding keeps provider preference and leaves loopback last.</summary>
    [TestMethod]
    public void OrderBoundCandidateUrls_UnorderedEndpoints_PreservesPreferredAdapterOrder()
    {
        // Arrange
        var physical = new Uri("http://192.168.1.8:41001");
        var virtualAdapter = new Uri("http://172.19.208.1:41002");
        var loopback = new Uri("http://127.0.0.1:41003");
        NetworkAddressCandidate[] requestedCandidates =
        [
            new NetworkAddressCandidate(IPAddress.Parse(physical.Host), "WLAN"),
            new NetworkAddressCandidate(IPAddress.Parse(virtualAdapter.Host), "vEthernet (WSL)"),
        ];

        // Act
        ImmutableArray<Uri> ordered = RemoteHost.OrderBoundCandidateUrls(
            [virtualAdapter, loopback, physical],
            requestedCandidates);

        // Assert
        Assert.HasCount(3, ordered);
        Assert.AreEqual(physical, ordered[0]);
        Assert.AreEqual(virtualAdapter, ordered[1]);
        Assert.AreEqual(loopback, ordered[2]);
    }

    /// <summary>Verifies every bound adapter becomes a distinct labeled choice with an exact QR payload.</summary>
    [TestMethod]
    public void CreatePairingDescriptor_MultipleAdapters_MapsLabelsAndExactUris()
    {
        // Arrange
        const string token = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";
        ImmutableArray<Uri> urls =
        [
            new Uri("http://10.20.30.40:41001"),
            new Uri("http://192.168.50.20:41002"),
        ];
        NetworkAddressCandidate[] candidates =
        [
            new NetworkAddressCandidate(IPAddress.Parse("192.168.50.20"), "Wi-Fi"),
            new NetworkAddressCandidate(IPAddress.Parse("10.20.30.40"), "Ethernet"),
        ];

        // Act
        PresentationTimer.Core.Models.DesktopPairingDescriptor descriptor =
            RemoteHost.CreatePairingDescriptor(urls, candidates, token);

        // Assert
        Assert.HasCount(2, descriptor.Candidates);
        Assert.AreEqual("Ethernet", descriptor.Candidates[0].InterfaceLabel);
        Assert.AreEqual("Wi-Fi", descriptor.Candidates[1].InterfaceLabel);
        Assert.AreNotEqual(
            descriptor.Candidates[0].PairingUri,
            descriptor.Candidates[1].PairingUri);
        foreach (PresentationTimer.Core.Models.DesktopPairingCandidate candidate in descriptor.Candidates)
        {
            Assert.AreEqual(candidate.PairingUri.AbsoluteUri, candidate.QrPayload);
            Assert.AreEqual(
                token,
                QueryHelpers.ParseQuery(candidate.PairingUri.Query)["t"].ToString());
            Assert.IsGreaterThan(100, candidate.QrPng.Length);
        }
    }
}
