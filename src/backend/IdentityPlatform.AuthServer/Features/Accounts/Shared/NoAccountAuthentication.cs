using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Prevents ASP.NET Core's sole-scheme default from implicitly selecting administrator authentication.</summary>
internal sealed class NoAccountAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Returns no identity for implicit authentication; future protected pages must name their scheme explicitly.</summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}
