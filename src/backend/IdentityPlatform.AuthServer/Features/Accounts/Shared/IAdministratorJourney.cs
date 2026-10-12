namespace IdentityPlatform.AuthServer.Features.Accounts;

/// <summary>Provides the Razor boundary for browser-bound administrator enrollment and MFA login.</summary>
/// <remarks>Methods enforce exact forms and committed authority. Callers must retain native Razor antiforgery protection.</remarks>
/// <example><code>var location = await journey.BeginAsync(HttpContext, enrollment: true);</code></example>
public interface IAdministratorJourney
{
    /// <summary>Validates a password form and returns a fixed local continuation only after restricted admission commits.</summary>
    /// <example><code>var location = await journey.BeginAsync(HttpContext, enrollment: false); // No session is issued.</code></example>
    Task<string?> BeginAsync(HttpContext context, bool enrollment);

    /// <summary>Confirms the native unused TOTP, issues a committed session and returns one-time recovery codes for enrollment.</summary>
    /// <example><code>var codes = await journey.CompleteAsync(HttpContext, enrollment: true); // Render once; never persist or log.</code></example>
    Task<IReadOnlyList<string>?> CompleteAsync(HttpContext context, bool enrollment);

    /// <summary>Reads the browser-bound staged key for enrollment display without committing GET-side state.</summary>
    /// <example><code>var seed = await journey.StagedAsync(HttpContext); // Never cache, log or capture this response.</code></example>
    Task<string?> StagedAsync(HttpContext context);
}
