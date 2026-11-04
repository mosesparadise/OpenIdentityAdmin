using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Supplies independently bounded synthetic storage rows; these are never accepted as real password, MFA, consent or native issuance proofs.</summary>
internal static class ManagedSchemaRows
{
    internal const long Created = 1000;
    internal const long Expires = 1300;
    internal const long Retention = 1_210_600;
    internal const string ApplicationId = "synthetic-managed-application";
    private const string Profile = "managed-web-v1";
    private const string IdentifierFormat = "N";
    private const string User = "synthetic-managed-account";
    private const string Stamp = "synthetic-security-stamp";
    private const long PasswordTime = 1010;
    private const long MfaTime = 1020;
    private const long ConsentTime = 1030;
    private const int NativeIdentifierLength = 400;
    private const int AccountLength = 450;
    private const int StampLength = 256;

    /// <summary>Uses maximum supported native identifiers and account fields to catch accidental narrowing in either provider.</summary>
    internal static ManagedLoginInteraction Boundaries()
    {
        var row = Interaction(ManagedLoginState.Approved);
        row.ApplicationId = new string('a', NativeIdentifierLength);
        row.RequestId = new string('r', NativeIdentifierLength);
        row.UserId = new string('u', AccountLength);
        row.SecurityStamp = new string('s', StampLength);
        return row;
    }

    /// <summary>Creates unique bounded digests for relational uniqueness tests without implying possession of any browser proof.</summary>
    internal static string Digest() => Guid.NewGuid().ToString(IdentifierFormat) + Guid.NewGuid().ToString(IdentifierFormat);

    /// <summary>Returns valid expired-but-inspectable lifecycle metadata with independent aggregate and credential versions.</summary>
    internal static ApplicationPolicy Policy() => new()
    {
        ApplicationId = ApplicationId,
        Profile = Profile,
        Revision = 1,
        CredentialRevision = 1,
        CredentialIssuedAt = Created,
        CredentialExpiresAt = Expires,
        ObservedAt = Expires + 1
    };

    /// <summary>Allows the temporary pre-cache reservation shape that a later owned commit must attach to a real native row.</summary>
    internal static ManagedLoginInteraction Reservation() => new()
    {
        SelectorDigest = Digest(),
        BrowserDigest = Digest(),
        DemandDigest = Digest(),
        ApplicationId = ApplicationId,
        ApplicationRevision = 1,
        Revision = 1,
        State = ManagedLoginState.PendingPassword,
        CreatedAt = Created,
        ExpiresAt = Expires,
        ObservedAt = Created,
        RetainUntil = Retention,
        CorrelationId = Guid.NewGuid()
    };

    /// <summary>Builds only static phase-consistent rows, with actual runtime verification deliberately outside this schema fixture.</summary>
    internal static ManagedLoginInteraction Interaction(ManagedLoginState state)
    {
        var row = Reservation();
        row.RequestId = Digest();
        row.RequestExpiresAt = Expires;
        row.NativeDependencyCreatedAt = Created;
        row.NativeDependencyExpiresAt = Expires;
        row.State = state;
        if (state is ManagedLoginState.PendingPassword or ManagedLoginState.Denied or ManagedLoginState.Expired)
        {
            return row;
        }
        row.CeremonyId = Guid.NewGuid();
        row.CeremonyBrowserDigest = Digest();
        row.BrowserBudgetId = Digest();
        row.UserId = User;
        row.SecurityStamp = Stamp;
        row.AuthenticationGeneration = row.FactorGeneration = 1;
        row.EnrollmentRevision = 0;
        row.PasswordVerifiedAt = PasswordTime;
        if (state != ManagedLoginState.PendingMfa)
        {
            row.MfaVerifiedAt = MfaTime;
        }
        if (state is ManagedLoginState.Approved or ManagedLoginState.Consumed)
        {
            row.ConsentVerifiedAt = ConsentTime;
        }
        if (state == ManagedLoginState.Consumed)
        {
            row.CodeId = Digest();
            row.AuthorizationId = Digest();
        }
        return row;
    }
}
