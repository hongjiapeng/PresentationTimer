using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PresentationTimer.Remote.Security;

namespace PresentationTimer.Remote.Authentication;

internal static class SessionAuthenticationDefaults
{
    internal const string Scheme = "PresenterSession";
}

internal sealed class SessionCookieAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly SessionCredentialStore _credentials;

    public SessionCookieAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SessionCredentialStore credentials)
        : base(options, logger, encoder)
    {
        this._credentials = credentials;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Cookies.TryGetValue(SessionCredentialStore.CookieName, out string? credential) ||
            !this._credentials.ValidateBrowserCredential(credential))
        {
            return Task.FromResult(AuthenticateResult.Fail("The presenter session is invalid or expired."));
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "paired-presenter") },
            SessionAuthenticationDefaults.Scheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            SessionAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
