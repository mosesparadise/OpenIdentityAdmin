namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Names fixed server-selected account actions; request fields cannot select a more privileged purpose.</summary>
/// <example><code>await journey.BeginAsync(HttpContext, AdministratorSecurityAction.Recovery);</code></example>
public enum AdministratorSecurityAction
{
    Recovery, Replacement, Regeneration
}

/// <summary>Adapts protected Razor requests to restricted recovery and current-session security operations.</summary>
/// <remarks>Keep native Razor antiforgery enabled. Returned codes are one-time response material and must never be logged or cached.</remarks>
/// <example><code>var location = await journey.BeginAsync(HttpContext, AdministratorSecurityAction.Replacement);</code></example>
public interface IAdministratorSecurityJourney
{
    /// <summary>Admits an exact fresh-password form for the server-selected action without issuing a full session.</summary>
    /// <example><code>var next = await journey.BeginAsync(HttpContext, AdministratorSecurityAction.Recovery);</code></example>
    Task<string?> BeginAsync(HttpContext context, AdministratorSecurityAction action);

    /// <summary>Consumes native recovery or current-factor proof and returns the committed restricted replacement location.</summary>
    /// <example><code>var next = await journey.AuthorizeAsync(HttpContext, recovery: true); // No full session is created.</code></example>
    Task<string?> AuthorizeAsync(HttpContext context, bool recovery);

    /// <summary>Completes proven replacement or regeneration and returns new recovery codes only after issuing committed authority.</summary>
    /// <example><code>var codes = await journey.CompleteAsync(HttpContext, regeneration: false); // Render once only.</code></example>
    Task<IReadOnlyList<string>?> CompleteAsync(HttpContext context, bool regeneration);

    /// <summary>Reads only a native-authorized, browser-bound replacement key without committing GET-side changes.</summary>
    /// <example><code>var seed = await journey.StagedAsync(HttpContext); // Never persist or capture this response.</code></example>
    Task<string?> StagedAsync(HttpContext context);

    /// <summary>Revokes the exact current stored session and audits before clearing its named browser cookie.</summary>
    /// <example><code>var revoked = await journey.LogoutAsync(HttpContext); // Invoke only from an antiforgery-protected POST.</code></example>
    Task<bool> LogoutAsync(HttpContext context);
}
