using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace PresentationTimer.Remote.Security;

/// <summary>
/// Owns ephemeral pairing and browser credentials while retaining only SHA-256 hashes.
/// </summary>
public sealed class SessionCredentialStore
{
    /// <summary>The name of the short-lived browser session cookie.</summary>
    public const string CookieName = "presentation_timer_session";

    private readonly object _gate = new object();
    private readonly List<byte[]> _browserCredentialHashes = new List<byte[]>();
    private byte[] _pairingTokenHash = Array.Empty<byte>();

    /// <summary>Creates a new session and returns its 256-bit base64url pairing token.</summary>
    /// <returns>The raw desktop-only pairing token.</returns>
    public string CreateSession()
    {
        lock (this._gate)
        {
            this.ClearCore();
            byte[] token = RandomNumberGenerator.GetBytes(32);
            try
            {
                this._pairingTokenHash = SHA256.HashData(token);
                return WebEncoders.Base64UrlEncode(token);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(token);
            }
        }
    }

    /// <summary>
    /// Exchanges a valid current pairing token for an independent browser credential.
    /// </summary>
    /// <param name="pairingToken">The token from the pairing URI.</param>
    /// <param name="browserCredential">The new raw browser credential when valid.</param>
    /// <returns><see langword="true"/> when the pairing token was valid.</returns>
    public bool TryExchangePairingToken(string? pairingToken, out string? browserCredential)
    {
        browserCredential = null;
        if (!TryDecodeToken(pairingToken, out byte[] decoded))
        {
            return false;
        }

        try
        {
            byte[] candidateHash = SHA256.HashData(decoded);
            lock (this._gate)
            {
                if (this._pairingTokenHash.Length == 0 ||
                    !CryptographicOperations.FixedTimeEquals(candidateHash, this._pairingTokenHash))
                {
                    return false;
                }

                byte[] credential = RandomNumberGenerator.GetBytes(32);
                try
                {
                    this._browserCredentialHashes.Add(SHA256.HashData(credential));
                    browserCredential = WebEncoders.Base64UrlEncode(credential);
                    return true;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(credential);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    /// <summary>Validates a browser credential using fixed-time hash comparison.</summary>
    /// <param name="browserCredential">The raw cookie value.</param>
    /// <returns><see langword="true"/> when the credential belongs to the live session.</returns>
    public bool ValidateBrowserCredential(string? browserCredential)
    {
        if (!TryDecodeToken(browserCredential, out byte[] decoded))
        {
            return false;
        }

        try
        {
            byte[] candidateHash = SHA256.HashData(decoded);
            lock (this._gate)
            {
                bool matched = false;
                foreach (byte[] storedHash in this._browserCredentialHashes)
                {
                    matched |= CryptographicOperations.FixedTimeEquals(candidateHash, storedHash);
                }

                return this._pairingTokenHash.Length != 0 && matched;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    /// <summary>Immediately invalidates pairing and browser credentials.</summary>
    public void Revoke()
    {
        lock (this._gate)
        {
            this.ClearCore();
        }
    }

    private static bool TryDecodeToken(string? value, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            decoded = WebEncoders.Base64UrlDecode(value);
            if (decoded.Length == 32)
            {
                return true;
            }

            CryptographicOperations.ZeroMemory(decoded);
            decoded = Array.Empty<byte>();
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void ClearCore()
    {
        if (this._pairingTokenHash.Length != 0)
        {
            CryptographicOperations.ZeroMemory(this._pairingTokenHash);
            this._pairingTokenHash = Array.Empty<byte>();
        }

        foreach (byte[] hash in this._browserCredentialHashes)
        {
            CryptographicOperations.ZeroMemory(hash);
        }

        this._browserCredentialHashes.Clear();
    }
}
