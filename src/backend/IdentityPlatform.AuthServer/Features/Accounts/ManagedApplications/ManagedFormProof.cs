using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;

/// <summary>Protects finite managed form intent in a distinct purpose, binding every phase to its original browser and native request.</summary>
internal static class ManagedFormProof
{
    internal const string Field = "Flow";
    internal const string Path = "/account/application/sign-in";
    private const string Purpose = "IdentityPlatform.ManagedApplications.Form.v1";
    private const int MaximumLength = 4096;

    /// <summary>Issues an opaque form handle only for server-derived state; it cannot authenticate a user or complete native issuance.</summary>
    internal static string Protect(HttpContext http, ManagedHumanRequest request) =>
        http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect(JsonSerializer.Serialize(request));

    /// <summary>Rejects cross-browser, expired, corrupt and oversized handles before database or credential work.</summary>
    internal static ManagedHumanRequest? Read(HttpContext http, string? value)
    {
        if (value is not { Length: > 0 and <= MaximumLength } || ManagedBrowser.Read(http) is not { } browser)
        {
            return null;
        }
        try
        {
            var request = JsonSerializer.Deserialize<ManagedHumanRequest>(http.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(value));
            return request is not null && ManagedLoginRules.IsSecret(request.Selector) && CeremonySecrets.IsIdentifier(request.Correlation) &&
                request.BrowserDigest == ManagedLoginRules.Digest(browser) && request.Revision > 0 &&
                request.ExpiresAt > http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds() ? request : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }

    /// <summary>Advances only server-proven phase/revision while preserving original selector, browser, native intent and absolute deadline.</summary>
    internal static ManagedHumanRequest Advance(ManagedHumanRequest prior, ManagedHumanResult result) => new()
    {
        Selector = prior.Selector,
        BrowserDigest = prior.BrowserDigest,
        ClientId = prior.ClientId,
        Callback = prior.Callback,
        DemandDigest = prior.DemandDigest,
        RequestId = prior.RequestId,
        RequestUri = prior.RequestUri,
        Correlation = prior.Correlation,
        ExpiresAt = prior.ExpiresAt,
        State = result.State,
        Revision = result.Revision
    };
}
