namespace IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;

internal enum ProtocolAdmissionOutcome
{
    Allowed, LoginRequired, AccessDenied
}

/// <summary>Contains a transaction-local proof only when all native current-authority checks succeeded.</summary>
internal sealed record ProtocolAdmissionResult(ProtocolAdmissionOutcome Outcome, ProtocolAdmissionProof? Proof);

/// <summary>Supplies finite native claim inputs only; issuance must still hold and finally check the original live proof.</summary>
internal sealed record ProtocolAdmissionClaims(string UserId, string SessionId, long PasswordVerifiedAt, long FactorVerifiedAt,
    long SourceExpiresAt, long CodeExpiresAt, long TokenExpiresAt);
