using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks strict password/factor admission deadlines independently of the persistence and native issuance fixtures.</summary>
public sealed class ProtocolAdmissionTimeTests
{
    private const long Now = 10_000;

    /// <summary>Neither the five-minute boundary nor a shorter requested age permits equality or future authentication proof.</summary>
    [Theory]
    [InlineData(299, null, true)]
    [InlineData(300, null, false)]
    [InlineData(301, null, false)]
    [InlineData(1, 1L, false)]
    [InlineData(0, 0L, false)]
    [InlineData(0, 1L, true)]
    [InlineData(299, 600L, true)]
    [InlineData(300, 600L, false)]
    [InlineData(0, -1L, false)]
    [InlineData(-1, null, false)]
    public void Given_OriginalProofAge_When_AdmissionIsEvaluated_Then_StrictSmallerCeilingApplies(long age, long? requested, bool expected)
    {
        var session = new AdministratorSession { PasswordVerifiedAt = Now - age, FactorVerifiedAt = Now - age };
        Assert.Equal(expected, ProtocolAdmissionTime.Allows(session, Now, requested));
    }

    /// <summary>A recent factor cannot renew the original password proof, and reversed factor/password ordering is invalid.</summary>
    [Theory]
    [InlineData(300, 1)]
    [InlineData(1, 300)]
    public void Given_OneStaleProof_When_TheOtherIsRecent_Then_AdmissionStillFails(long passwordAge, long factorAge)
    {
        var session = new AdministratorSession { PasswordVerifiedAt = Now - passwordAge, FactorVerifiedAt = Now - factorAge };
        Assert.False(ProtocolAdmissionTime.Allows(session, Now, null));
    }

    /// <summary>Code expiry independently honors its own limit, source expiry and original shorter freshness deadline.</summary>
    [Theory]
    [InlineData(0, null, 1000, 60)]
    [InlineData(299, null, 1000, 1)]
    [InlineData(5, 10L, 1000, 5)]
    [InlineData(0, null, 1, 1)]
    public void Given_CurrentAdmission_When_CodeDeadlineIsDerived_Then_NoOriginalBoundIsExtended(long age, long? requested, long sourceRemaining, long expected)
    {
        var session = new AdministratorSession { PasswordVerifiedAt = Now - age, FactorVerifiedAt = Now, ExpiresAt = Now + sourceRemaining };
        Assert.True(ProtocolAdmissionTime.Allows(session, Now, requested));
        Assert.Equal(Now + expected, ProtocolAdmissionTime.CodeExpiry(session, Now, requested));
    }

    /// <summary>Public token lifetime is capped by the source session even when it is shorter than five minutes.</summary>
    [Theory]
    [InlineData(1000, 300)]
    [InlineData(1, 1)]
    public void Given_OriginalSessionExpiry_When_PublicTokenDeadlineIsDerived_Then_SourceExpiryIsPreserved(long remaining, long expected)
    {
        var session = new AdministratorSession { ExpiresAt = Now + remaining };
        Assert.Equal(Now + expected, ProtocolAdmissionTime.TokenExpiry(session, Now));
    }
}
