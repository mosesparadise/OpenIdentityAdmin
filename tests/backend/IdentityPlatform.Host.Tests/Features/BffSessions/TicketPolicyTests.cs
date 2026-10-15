using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Host.Tests.Features.BffSessions;

/// <summary>Exercises canonical native binding and lifetime ownership without database substitutes.</summary>
public sealed class TicketPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ShortIdle = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Absolute = TimeSpan.FromHours(8);
    private const string SubjectClaim = "sub";
    private const string SessionClaim = "sid";
    /// <summary>Names independent canonical native-binding violations.</summary>
    public enum BindingDamage
    {
        Scheme, Authentication, Subject, Session, Duplicate, Identity
    }
    private const string Subject = "synthetic-subject";
    private const string Session = "synthetic-session";

    /// <summary>Requires a retained shorter idle policy and rejects equality and clock rollback at tick precision.</summary>
    [Fact]
    public void Given_ShortIdlePolicy_When_Renewed_Then_OriginalDurationAndAbsoluteCapSurvive()
    {
        var row = SessionTicketPolicy.Create(SessionHandle.Create().Digest, Subject, Session,
            Start, Start.Add(Absolute), ShortIdle, Absolute);
        Assert.True(SessionTicketPolicy.IsLive(row, Start.Add(ShortIdle).AddTicks(-1)));
        Assert.False(SessionTicketPolicy.IsLive(row, Start.Add(ShortIdle)));
        Assert.False(SessionTicketPolicy.IsLive(row, Start.AddTicks(-1)));
        const int ActivityMinutes = 4;
        const int RenewedExpiryMinutes = 9;
        var activity = Start.AddMinutes(ActivityMinutes);
        var renewed = SessionTicketPolicy.Renew(row, activity);
        Assert.Equal(Start.AddMinutes(RenewedExpiryMinutes).UtcTicks, renewed.IdleExpiresAt);
        Assert.Equal(row.AbsoluteExpiresAt, renewed.AbsoluteExpiresAt);
        Assert.Equal(row.IdleLifetime, renewed.IdleLifetime);
        Assert.Equal(2, renewed.Revision);
        Assert.False(SessionTicketPolicy.IsLive(renewed, activity.AddTicks(-1)));
    }

    /// <summary>Rejects mismatched native scheme, claims and additional identities before persistence.</summary>
    [Theory]
    [InlineData(BindingDamage.Scheme)]
    [InlineData(BindingDamage.Authentication)]
    [InlineData(BindingDamage.Subject)]
    [InlineData(BindingDamage.Session)]
    [InlineData(BindingDamage.Duplicate)]
    [InlineData(BindingDamage.Identity)]
    public void Given_InconsistentNativeBinding_When_Validated_Then_TicketIsRejected(BindingDamage damage)
    {
        const string Other = "other";
        var identity = new ClaimsIdentity([new Claim(SubjectClaim, Subject), new Claim(SessionClaim, Session)],
            damage == BindingDamage.Authentication ? Other : SessionTicketPolicy.Scheme);
        if (damage == BindingDamage.Duplicate)
        {
            identity.AddClaim(new Claim(SubjectClaim, Subject));
        }
        if (damage == BindingDamage.Subject)
        {
            identity.RemoveClaim(identity.FindFirst(SubjectClaim)!);
        }
        if (damage == BindingDamage.Session)
        {
            identity.RemoveClaim(identity.FindFirst(SessionClaim)!);
        }
        var principal = new ClaimsPrincipal(identity);
        if (damage == BindingDamage.Identity)
        {
            principal.AddIdentity(new ClaimsIdentity());
        }
        var ticket = new AuthenticationTicket(principal, damage == BindingDamage.Scheme ? Other : SessionTicketPolicy.Scheme);
        Assert.False(SessionTicketPolicy.MatchesBinding(ticket, Subject, Session));
    }

    /// <summary>Rejects zero, negative and above-maximum requested lifetimes rather than silently widening policy.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(31, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 9)]
    public void Given_InvalidLifetime_When_Created_Then_InputIsRejected(int idleMinutes, int absoluteHours) =>
        Assert.Throws<ArgumentException>(() => SessionTicketPolicy.Create(SessionHandle.Create().Digest,
            Subject, Session, Start, Start.Add(Absolute), TimeSpan.FromMinutes(idleMinutes), TimeSpan.FromHours(absoluteHours)));

    /// <summary>Requires strict canonical handles so equivalent encodings cannot acquire a second lookup representation.</summary>
    [Fact]
    public void Given_FreshHandles_When_Validated_Then_ReferencesAreDistinctCanonicalAndHashed()
    {
        const int RejectedHandleLength = 44;
        const string Padding = "=";
        var first = SessionHandle.Create();
        var second = SessionHandle.Create();
        Assert.NotEqual(first.Handle, second.Handle);
        Assert.Equal(43, first.Handle.Length);
        Assert.Equal(64, first.Digest.Length);
        Assert.True(SessionHandle.TryDigest(first.Handle, out var digest));
        Assert.Equal(first.Digest, digest);
        Assert.False(SessionHandle.TryDigest(first.Handle + Padding, out _));
        Assert.False(SessionHandle.TryDigest(new string('A', RejectedHandleLength), out _));
    }
}
