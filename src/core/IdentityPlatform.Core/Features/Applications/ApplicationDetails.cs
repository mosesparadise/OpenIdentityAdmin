using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Returns only selected registration fields and credential dates; protected native registrations have no editable managed profile.</summary>
/// <example><code>var details = new ApplicationDetails(id, clientId, "Example", ["https://client.example/callback"], ApplicationEditorRules.Profile, true, metadata, authorityExpiry);</code></example>
public sealed record ApplicationDetails(
    [property: MinLength(1), MaxLength(ApplicationProjection.IdentifierLength)] string Id,
    [property: MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: MaxLength(ApplicationProjection.DisplayNameLength)] string? DisplayName,
    [property: MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris,
    string? Profile, bool Editable, ApplicationCredentialMetadata? Credential, DateTimeOffset ExpiresAt);

/// <summary>Exposes credential lifecycle metadata without the native hash, generated plaintext or arbitrary native properties.</summary>
/// <example><code>var metadata = new ApplicationCredentialMetadata(issuedAt, expiresAt);</code></example>
public sealed record ApplicationCredentialMetadata(DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);

/// <summary>Holds one successful mutation's secret only for response serialization; ordinary diagnostic formatting never includes its value.</summary>
/// <example><code>// Serialize only after the native mutation and audit have committed and cleaned up.
/// return Results.Json(new ApplicationCredentialOutput(details, generatedSecret));</code></example>
public sealed class ApplicationCredentialOutput
{
    public ApplicationDetails Application
    {
        get;
    }
    public string ClientSecret
    {
        get;
    }

    /// <summary>Retains the generated value in the immediate response only; callers must never cache, log or replay this object.</summary>
    /// <example><code>var output = new ApplicationCredentialOutput(details, generatedSecret); // Send once over the protected response.</code></example>
    public ApplicationCredentialOutput(ApplicationDetails application, string clientSecret)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        Application = application;
        ClientSecret = clientSecret;
    }
}
