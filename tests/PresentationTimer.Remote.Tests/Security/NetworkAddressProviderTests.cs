using System.Collections.Immutable;
using System.Net;
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
