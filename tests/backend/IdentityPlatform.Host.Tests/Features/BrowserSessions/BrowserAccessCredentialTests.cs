using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks protected access-credential binding and original expiry without claiming native callback validation.</summary>
public sealed class BrowserAccessCredentialTests
{
    private const int DigestLength = 64;
    private const int AccessMinutes = 5;
    private const int CurrentMinute = 4;
    private const int SessionHours = 8;
    private const int IdleMinutes = 30;
    private const int StoredCredentials = 2;
    private const int OverlongCredential = 16385;
    private const string SubjectClaim = "sub";
    private const string SessionClaim = "sid";
    private const string AccessName = "access_token";
    private const string ExpiryName = "expires_at";
    private const string IdentityName = "id_token";
    private const string RefreshName = "refresh_token";
    private const string Subject = "subject";
    private const string Session = "session";
    private const string Access = "synthetic-access-credential";
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Protected ticket roundtrip retains only the exact access credential and expiration, never renews the original deadline.</summary>
    [Fact]
    public void Given_AccessCredential_When_ProtectedAndRead_Then_OriginalValueAndExpiryRemainBound()
    {
        var ticket = Ticket();
        var credential = BrowserAccessCredential.Capture(Access, Now.AddMinutes(AccessMinutes), Now);
        Assert.NotNull(credential);
        credential.Store(ticket.Properties);
        var row = SessionTicketPolicy.Create(new string('A', DigestLength), Subject, Session, Now, Now.AddHours(SessionHours),
            TimeSpan.FromMinutes(IdleMinutes), TimeSpan.FromHours(SessionHours));
        var codec = new SessionTicketCodec(new EphemeralDataProtectionProvider());
        row.ProtectedTicket = codec.Protect(row, ticket);
        var restored = codec.Unprotect(row)!;
        var read = BrowserAccessCredential.Read(restored.Properties, Now.AddMinutes(CurrentMinute));
        Assert.NotNull(read);
        Assert.Equal(Access, read.Value);
        Assert.Equal(Now.AddMinutes(AccessMinutes), read.ExpiresAt);
        Assert.Null(BrowserAccessCredential.Read(restored.Properties, Now.AddMinutes(AccessMinutes)));
        Assert.Equal(StoredCredentials, restored.Properties.GetTokens().Count());
        Assert.Null(restored.Properties.GetTokenValue(IdentityName));
        Assert.Null(restored.Properties.GetTokenValue(RefreshName));
        Assert.False(restored.Properties.AllowRefresh);
    }

    /// <summary>Legacy status-only tickets have no management credential and cannot borrow identity claims as access.</summary>
    [Fact]
    public void Given_StatusOnlyTicket_When_ManagementCredentialRequested_Then_NoCredentialIsReturned() =>
        Assert.Null(BrowserAccessCredential.Read(Ticket().Properties, Now));

    /// <summary>Missing, overlong, expired and noncanonical transport values cannot enter protected management state.</summary>
    [Theory]
    [InlineData(null, 60)]
    [InlineData("", 60)]
    [InlineData("has space", 60)]
    [InlineData("line\r\nbreak", 60)]
    [InlineData(Access, 0)]
    [InlineData(Access, -1)]
    [InlineData(Access, 301)]
    public void Given_InvalidCredential_When_Captured_Then_NoCredentialIsProduced(string? value, int seconds) =>
        Assert.Null(BrowserAccessCredential.Capture(value, Now.AddSeconds(seconds), Now));

    /// <summary>Overlong access values and absent expiration fail before serialization or forwarding.</summary>
    [Fact]
    public void Given_UnboundedOrUndatedAccess_When_Captured_Then_NoCredentialIsProduced()
    {
        Assert.Null(BrowserAccessCredential.Capture(new string('a', OverlongCredential), Now.AddMinutes(AccessMinutes), Now));
        Assert.Null(BrowserAccessCredential.Capture(Access, null, Now));
    }

    /// <summary>Malformed expiration cannot be parsed leniently into a new lifetime.</summary>
    [Theory]
    [InlineData("2026-10-29")]
    [InlineData("2026-10-29T12:05:00Z")]
    [InlineData("not-a-date")]
    public void Given_MalformedStoredExpiry_When_Read_Then_NoCredentialIsProduced(string expiry)
    {
        var properties = Ticket().Properties;
        properties.StoreTokens([new() { Name = AccessName, Value = Access }, new() { Name = ExpiryName, Value = expiry }]);
        Assert.Null(BrowserAccessCredential.Read(properties, Now));
    }

    /// <summary>Creates only canonical local subject/session claims, independently of callback provenance.</summary>
    private static AuthenticationTicket Ticket() => new(new ClaimsPrincipal(new ClaimsIdentity(
        [new(SubjectClaim, Subject), new(SessionClaim, Session)], SessionTicketPolicy.Scheme)),
        new AuthenticationProperties { AllowRefresh = false, IsPersistent = false }, SessionTicketPolicy.Scheme);
}
