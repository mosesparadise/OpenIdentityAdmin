using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Retains a local committed reservation privately until its outer scope has disposed; raw correlation is never persisted.</summary>
internal sealed class BrowserLogoutRequest(string state, BrowserLogoutReservation reservation)
{
    private const int HttpsPort = 443;
    internal const string Choice = "Mode";
    internal const string Local = "local";
    internal const string Issuer = "issuer";
    internal string State { get; } = state;
    internal BrowserLogoutReservation Reservation { get; } = reservation;

    /// <summary>Accepts only an exact canonical HTTPS state-only return before protected cookies or durable state are consulted.</summary>
    internal static async Task<string?> ReturnAsync(HttpContext http, BrowserSessionDestinations destinations)
    {
        var request = http.Request;
        return BrowserLoginRequest.Get(request) && request.ContentType is null && request.PathBase == PathString.Empty &&
            request.Path.Value == BrowserSessionDestinations.LogoutReturnPath &&
            request.Host.Host == destinations.Client.Host && (request.Host.Port ?? HttpsPort) == destinations.Client.Port &&
            request.Query.Count == 1 && request.Query.Keys.Single() == OpenIddict.Abstractions.OpenIddictConstants.Parameters.State &&
            request.Query[OpenIddict.Abstractions.OpenIddictConstants.Parameters.State] is { Count: 1 } value &&
            BrowserLoginSelector.Digest(value[0]) is not null && await BrowserLoginRequest.EmptyAsync(http) ? value[0] : null;
    }
}
