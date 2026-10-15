using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.BffSessions;

/// <summary>Preserves exact native binding, source-session ceilings and fail-closed configuration boundaries.</summary>
public sealed class TicketBoundaryTests
{
    private const string SubjectType = "sub";
    private const string UpperSubjectType = "SUB";
    private const string SourceType = "sid";
    private const string UpperSourceType = "SID";
    private const string OtherBinding = "synthetic-other-binding";

    /// <summary>Rejects case aliases, wrong replacements and actor identities before serializing a new or replacement ticket.</summary>
    [Theory]
    [InlineData("SUB", "sid")]
    [InlineData("sub", "SID")]
    [InlineData("SUB", "SID")]
    public void Given_CaseVariantBinding_When_Protected_Then_NativeAliasesCannotSatisfyCanonicalClaims(string subjectType, string sessionType)
    {
        using var fixture = new BffSessionFixture();
        using var protection = fixture.Protection();
        var codec = new SessionTicketCodec(protection.Provider);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(subjectType, BffSessionFixture.Subject),
            new Claim(sessionType, BffSessionFixture.SourceSession)], SessionTicketPolicy.Scheme));
        var ticket = new AuthenticationTicket(principal, SessionTicketPolicy.Scheme);
        Assert.Throws<ArgumentException>(() => codec.Protect(BffSessionFixture.Row(), ticket));
    }

    /// <summary>Rejects duplicate aliases and delegated actor identities even when the ordinary canonical claims are present.</summary>
    [Fact]
    public void Given_AdditionalAliasOrActor_When_BindingValidated_Then_ExactlyOneCanonicalIdentityIsRequired()
    {
        var ticket = BffSessionFixture.Ticket();
        var identity = Assert.IsType<ClaimsIdentity>(ticket.Principal.Identity);
        identity.AddClaim(new Claim(UpperSubjectType, BffSessionFixture.Subject));
        Assert.False(SessionTicketPolicy.MatchesBinding(ticket, BffSessionFixture.Subject, BffSessionFixture.SourceSession));
        identity.RemoveClaim(identity.Claims.Single(claim => claim.Type == UpperSubjectType));
        identity.Actor = new ClaimsIdentity([new Claim(SubjectType, OtherBinding)], SessionTicketPolicy.Scheme);
        Assert.False(SessionTicketPolicy.MatchesBinding(ticket, BffSessionFixture.Subject, BffSessionFixture.SourceSession));
    }

    /// <summary>Requires replacement tickets to retain exact subject/source binding and prevents revision overflow.</summary>
    [Fact]
    public void Given_ChangedReplacementOrMaximumRevision_When_Renewed_Then_NoSuccessorIsCreated()
    {
        using var fixture = new BffSessionFixture();
        using var protection = fixture.Protection();
        var row = BffSessionFixture.Row();
        var replacement = BffSessionFixture.Ticket();
        var identity = Assert.IsType<ClaimsIdentity>(replacement.Principal.Identity);
        identity.RemoveClaim(identity.Claims.Single(claim => claim.Type == SourceType));
        identity.AddClaim(new Claim(SourceType, OtherBinding));
        Assert.Throws<ArgumentException>(() => new SessionTicketCodec(protection.Provider).Protect(row, replacement));
        row.Revision = long.MaxValue;
        Assert.Throws<ArgumentException>(() => SessionTicketPolicy.Renew(row, BffSessionFixture.Start));
    }

    /// <summary>Requires immutable upstream expiry even when the source session ends before the requested idle window.</summary>
    [Fact]
    public void Given_EarlierSourceExpiry_When_Renewed_Then_AbsoluteCeilingCannotMove()
    {
        var ceiling = BffSessionFixture.Start.AddMinutes(1);
        var row = SessionTicketPolicy.Create(SessionHandle.Create().Digest, BffSessionFixture.Subject, BffSessionFixture.SourceSession,
            BffSessionFixture.Start, ceiling, BffSessionFixture.Idle, BffSessionFixture.Absolute);
        Assert.Equal(ceiling.UtcTicks, row.AbsoluteExpiresAt);
        var renewed = SessionTicketPolicy.Renew(row, ceiling.AddTicks(-1));
        Assert.Equal(ceiling.UtcTicks, renewed.IdleExpiresAt);
        Assert.False(SessionTicketPolicy.IsLive(renewed, ceiling));
        Assert.Throws<ArgumentException>(() => SessionTicketPolicy.Renew(renewed, ceiling));
    }

    /// <summary>Requires every scalar setting to remain a leaf even when its scalar value would otherwise be valid.</summary>
    [Theory]
    [InlineData("ApplicationId")]
    [InlineData("KeyRingPath")]
    [InlineData("Current:Path")]
    [InlineData("Current:Password")]
    [InlineData("Previous:0:Path")]
    [InlineData("Previous:0:Password")]
    public void Given_NestedScalarSetting_When_Initialized_Then_UnknownPolicyIsRejected(string field)
    {
        using var fixture = new BffSessionFixture();
        fixture.Material.RetainCurrentWrapper();
        var settings = fixture.Settings();
        const string Child = ":Unexpected";
        settings[BffSessionFixture.BffPrefix + field + Child] = OtherBinding;
        Assert.Throws<SessionProtectionException>(() => SessionProtection.Create(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }

    /// <summary>Shows the bounded writer stops before native serialization reaches later properties.</summary>
    [Fact]
    public void Given_OversizedEarlyClaim_When_NativeWriterRuns_Then_ItStopsBeforeLaterTicketFields()
    {
        const int TooLarge = 65_537;
        const string LargeClaim = "synthetic-large-claim";
        var ticket = BffSessionFixture.Ticket();
        Assert.IsType<ClaimsIdentity>(ticket.Principal.Identity).AddClaim(new Claim(LargeClaim, new string('x', TooLarge)));
        var tripwire = new UnreadableProperties();
        var guarded = new AuthenticationTicket(ticket.Principal, new AuthenticationProperties(tripwire), ticket.AuthenticationScheme);
        var failure = Assert.Throws<ArgumentException>(() => BoundedTicketSerialization.Serialize(guarded));
        Assert.False(tripwire.Observed);
        Assert.Equal("Native session ticket exceeds its size limit.", failure.Message);
    }
    /// <summary>Fails if native serialization reaches properties after an oversized early identity claim.</summary>
    private sealed class UnreadableProperties : Dictionary<string, string?>, IDictionary<string, string?>
    {
        private const string Failure = "Native serialization reached later fields.";
        internal bool Observed
        {
            get; private set;
        }
        int ICollection<KeyValuePair<string, string?>>.Count
        {
            /// <summary>Detects count inspection after the earlier size rejection must have stopped serialization.</summary>
            get
            {
                Observed = true;
                throw new InvalidOperationException(Failure);
            }
        }

        /// <summary>Detects enumeration as well as count access so later fields cannot be silently inspected.</summary>
        IEnumerator<KeyValuePair<string, string?>> IEnumerable<KeyValuePair<string, string?>>.GetEnumerator()
        {
            Observed = true;
            throw new InvalidOperationException(Failure);
        }
    }
}
