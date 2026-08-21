using Microsoft.AspNetCore.WebUtilities;
using PresentationTimer.Remote.Security;

namespace PresentationTimer.Remote.Tests.Security;

/// <summary>Verifies ephemeral pairing and browser credential lifecycle.</summary>
[TestClass]
public sealed class SessionCredentialStoreTests
{
    /// <summary>Verifies pairing tokens contain exactly 256 random bits and are unique.</summary>
    [TestMethod]
    public void CreateSession_Repeatedly_ReturnsUniqueThirtyTwoByteTokens()
    {
        // Arrange
        var credentials = new SessionCredentialStore();

        // Act
        string first = credentials.CreateSession();
        string second = credentials.CreateSession();

        // Assert
        Assert.HasCount(32, WebEncoders.Base64UrlDecode(first));
        Assert.HasCount(32, WebEncoders.Base64UrlDecode(second));
        Assert.AreNotEqual(first, second);
    }

    /// <summary>Verifies exchange creates a distinct valid HttpOnly-cookie credential value.</summary>
    [TestMethod]
    public void TryExchangePairingToken_WithCurrentToken_CreatesIndependentBrowserCredential()
    {
        // Arrange
        var credentials = new SessionCredentialStore();
        string pairingToken = credentials.CreateSession();

        // Act
        bool exchanged = credentials.TryExchangePairingToken(pairingToken, out string? browserCredential);

        // Assert
        Assert.IsTrue(exchanged);
        Assert.IsNotNull(browserCredential);
        Assert.AreNotEqual(pairingToken, browserCredential);
        Assert.IsTrue(credentials.ValidateBrowserCredential(browserCredential));
    }

    /// <summary>Verifies malformed and incorrect pairing tokens are rejected.</summary>
    [TestMethod]
    public void TryExchangePairingToken_WithInvalidValues_RejectsEveryValue()
    {
        // Arrange
        var credentials = new SessionCredentialStore();
        _ = credentials.CreateSession();
        var invalidValues = new List<string?> { null, string.Empty, "not-base64!", "AAAA" };

        // Act / Assert
        foreach (string? value in invalidValues)
        {
            Assert.IsFalse(credentials.TryExchangePairingToken(value, out string? credential));
            Assert.IsNull(credential);
        }
    }

    /// <summary>Verifies revocation invalidates pairing and browser credentials immediately.</summary>
    [TestMethod]
    public void Revoke_AfterExchange_InvalidatesEveryCredential()
    {
        // Arrange
        var credentials = new SessionCredentialStore();
        string pairingToken = credentials.CreateSession();
        Assert.IsTrue(credentials.TryExchangePairingToken(pairingToken, out string? browserCredential));

        // Act
        credentials.Revoke();

        // Assert
        Assert.IsFalse(credentials.TryExchangePairingToken(pairingToken, out _));
        Assert.IsFalse(credentials.ValidateBrowserCredential(browserCredential));
    }
}
