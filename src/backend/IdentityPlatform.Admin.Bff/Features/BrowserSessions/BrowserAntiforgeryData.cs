using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Binds native tokens to a request-owned durable browser snapshot; explicit future route setup must preload it asynchronously.</summary>
internal sealed class BrowserAntiforgeryData : IAntiforgeryAdditionalDataProvider
{
    private const string Unavailable = "Browser request binding is unavailable.";

    /// <summary>Refuses token issuance before a trusted snapshot exists; no synchronous database access or cookie-derived authority occurs here.</summary>
    public string GetAdditionalData(HttpContext context) =>
        context.Features.Get<BrowserRequestBinding>()?.AdditionalData ?? throw new InvalidOperationException(Unavailable);

    /// <summary>Rejects stale-generation or cross-browser tokens; the writer still compares the same expected generation under durable ownership.</summary>
    public bool ValidateAdditionalData(HttpContext context, string additionalData) =>
        context.Features.Get<BrowserRequestBinding>() is { } binding &&
        string.Equals(binding.AdditionalData, additionalData, StringComparison.Ordinal);
}
