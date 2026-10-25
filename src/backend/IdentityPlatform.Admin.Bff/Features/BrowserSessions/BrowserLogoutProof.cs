using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Reuses canonical pending-envelope parsing under an independent logout protection purpose, never as login authority.</summary>
internal sealed class BrowserLogoutProof(IDataProtectionProvider provider)
{
    private readonly BrowserLoginProof proofs = new(provider.CreateProtector(BrowserSessionCookies.LogoutPurpose));

    /// <summary>Protects the original random state and committed browser/generation/time binding without any issuer credentials.</summary>
    internal string Protect(BrowserPendingProof proof) => proofs.Protect(proof);

    /// <summary>Requires both independent browser ownership and exact original state; neither a query nor a cookie alone consumes a return.</summary>
    internal BrowserPendingProof? Read(string? value, string state, BrowserGenerationProof browser, DateTimeOffset now)
    {
        var proof = proofs.Pending(value, now);
        return proof is not null && proof.Intent == state && proof.BrowserDigest == browser.Digest && proof.Generation == browser.Generation &&
            proof.Created == browser.Created && proof.Expires == browser.Expires ? proof : null;
    }
}
