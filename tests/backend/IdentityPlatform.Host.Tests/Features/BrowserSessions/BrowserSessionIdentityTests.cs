using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Proves B3 signed identity shape, protected expectation and canonical B1 projection without claiming callback or persistence execution.</summary>
public sealed class BrowserSessionIdentityTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string Substitution = "synthetic-substitution";
    private const string ValuePlaceholder = "$value";
    private const int SourceLifetimeHours = 8;
    private const int IdleMinutes = 30;
    private const int DigestLength = 64;

    /// <summary>A native-validated identity becomes a fresh two-claim ticket whose protected expiry remains bounded by B1 and the original source.</summary>
    [Fact]
    public async Task Given_NativeSignedIdentity_When_Projected_Then_CanonicalTicketPreservesOriginalSourceDeadline()
    {
        var token = await BrowserSessionToken.CreateAsync(fixture);
        var binding = Assert.IsType<BrowserSessionIdentity>(Validate(token));
        Assert.Equal(token.Now.AddHours(SourceLifetimeHours), binding.SourceExpiresAt);
        var ticket = binding.CreateTicket();
        Assert.Equal(SessionTicketPolicy.Scheme, ticket.AuthenticationScheme);
        var identity = Assert.Single(ticket.Principal.Identities);
        Assert.Equal(SessionTicketPolicy.Scheme, identity.AuthenticationType);
        Assert.Null(identity.Actor);
        Assert.Equal(2, identity.Claims.Count());
        Assert.Equal(BrowserSessionToken.Subject, ticket.Principal.GetClaim(BrowserSessionToken.SubjectClaim));
        Assert.Equal(BrowserSessionToken.Session, ticket.Principal.GetClaim(BrowserSessionToken.SessionClaim));
        Assert.Empty(ticket.Properties.GetTokens());
        Assert.False(ticket.Properties.AllowRefresh);
        Assert.False(ticket.Properties.IsPersistent);
        Assert.NotSame(token.Principal, ticket.Principal);
        RequireProtectedTicket(binding, ticket, token.Now);
    }

    /// <summary>Signed malformed claims must deny even when native cryptography accepts and principal materialization normalizes their JSON.</summary>
    [Theory]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "\"$value\"")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "[$value]")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "$value.0")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "$valuee0")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "-1")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "9223372036854775807")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "true")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, "null")]
    [InlineData(BrowserSessionToken.SourceExpiryClaim, null)]
    [InlineData(BrowserSessionToken.IntentClaim, "[\"ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789\"]")]
    [InlineData(BrowserSessionToken.IntentClaim, "\"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789\"")]
    [InlineData(BrowserSessionToken.IntentClaim, "\"wrong\"")]
    [InlineData(BrowserSessionToken.IntentClaim, null)]
    [InlineData(BrowserSessionToken.SubjectClaim, "\"\"")]
    [InlineData(BrowserSessionToken.SessionClaim, "[\"synthetic-b3-session\"]")]
    [InlineData(BrowserSessionToken.SessionClaim, null)]
    [InlineData(BrowserSessionToken.AuthenticationTimeClaim, "\"$value\"")]
    [InlineData(BrowserSessionToken.AuthenticationTimeClaim, null)]
    [InlineData(BrowserSessionToken.MethodsClaim, "\"pwd otp\"")]
    [InlineData(BrowserSessionToken.MethodsClaim, "[\"pwd\",\"pwd\"]")]
    [InlineData(BrowserSessionToken.MethodsClaim, "[\"pwd\",\"otp\",\"otp\"]")]
    public async Task Given_SignedInvalidShape_When_Validated_Then_NoLocalIdentityIsProduced(string name, string? raw)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture, payload => InvalidShape(payload, name, raw));
        Assert.Null(Validate(token));
    }

    /// <summary>Native cryptography rejects array-shaped sub before the local adapter; rejection is never reported as a successful native principal.</summary>
    [Fact]
    public async Task Given_SignedArraySubject_When_NativeCryptoProcesses_Then_RejectionPrecedesProjection()
    {
        const string arraySubject = "[\"synthetic-b3-subject\"]";
        var token = await BrowserSessionToken.CreateAsync(fixture,
            payload => BrowserSessionToken.Field(payload, BrowserSessionToken.SubjectClaim, arraySubject), nativeRejectionExpected: true);
        Assert.Null(Validate(token));
    }

    /// <summary>Identical JSON duplicates and case aliases cannot be hidden by the native dictionary representation.</summary>
    [Theory]
    [InlineData(BrowserSessionToken.SubjectClaim, "\"synthetic-b3-subject\"")]
    [InlineData(BrowserSessionToken.SessionClaim, "\"synthetic-b3-session\"")]
    [InlineData(BrowserSessionToken.IntentClaim, "\"ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789\"")]
    [InlineData("ADMIN_LOGIN_INTENT", "\"ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789\"")]
    [InlineData(BrowserSessionToken.MethodsClaim, "[\"pwd\",\"otp\"]")]
    public async Task Given_SignedDuplicateOrAlias_When_NativeParsingNormalizes_Then_OriginalPayloadStillDenies(string name, string raw)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture, payload => payload[..^1] + ',' + System.Text.Json.JsonSerializer.Serialize(name) + ':' + raw + '}');
        Assert.Null(Validate(token));
    }

    /// <summary>A native identity cannot substitute its intent, source identity or claim types after cryptographic processing.</summary>
    [Theory]
    [InlineData(BrowserSessionToken.SubjectClaim)]
    [InlineData(BrowserSessionToken.SessionClaim)]
    [InlineData(BrowserSessionToken.SourceExpiryClaim)]
    [InlineData(BrowserSessionToken.IntentClaim)]
    [InlineData(BrowserSessionToken.AuthenticationTimeClaim)]
    [InlineData(BrowserSessionToken.MethodsClaim)]
    public async Task Given_ValidNativeOutput_When_PrincipalFieldIsSubstituted_Then_RawBindingRejects(string name)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture);
        Assert.NotNull(Validate(token));
        token.Principal.SetClaim(name, Substitution);
        Assert.Null(Validate(token));
    }

    /// <summary>Server expectation is mandatory and canonical; a valid signed token alone is insufficient to match a browser login intent.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456788")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    public async Task Given_ValidToken_When_ProtectedExpectationDoesNotMatch_Then_Denied(string expected)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture);
        Assert.Null(BrowserSessionIdentity.Validate(token.Principal, token.Encoded, expected, token.Now));
    }

    /// <summary>Source expiry equality denies; an earlier source ceiling is retained exactly instead of a fresh maximum allowance.</summary>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(600, true)]
    [InlineData(28801, false)]
    public async Task Given_OriginalSourceDeadline_When_AtPolicyBoundary_Then_NoLifetimeIsExtended(int seconds, bool accepted)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture, payload => ChangeDeadline(payload, seconds));
        var identity = Validate(token);
        Assert.Equal(accepted, identity is not null);
        if (accepted)
        {
            Assert.Equal(token.Now.AddSeconds(seconds), identity!.SourceExpiresAt);
        }
    }

    /// <summary>Freshness equality and excess future skew deny without substituting ID expiry or a newly computed source lifetime.</summary>
    [Theory]
    [InlineData(-301, false)]
    [InlineData(-300, false)]
    [InlineData(-299, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task Given_PasswordTime_When_AtFreshnessOrClockSkewBoundary_Then_StrictPolicyApplies(int seconds, bool accepted)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture, payload => ChangeDate(payload, BrowserSessionToken.AuthenticationTimeClaim, seconds));
        Assert.Equal(accepted, Validate(token) is not null);
    }

    /// <summary>Duplicate date properties deny even when their values exactly equal otherwise valid signed numeric dates.</summary>
    [Theory]
    [InlineData(BrowserSessionToken.SourceExpiryClaim)]
    [InlineData(BrowserSessionToken.AuthenticationTimeClaim)]
    public async Task Given_SignedDuplicateDate_When_NativeProjectionOverwrites_Then_RawMultiplicityDenies(string name)
    {
        var token = await BrowserSessionToken.CreateAsync(fixture, payload => DuplicateDate(payload, name));
        Assert.Null(Validate(token));
    }

    /// <summary>Shares the same validation call across adversarial cases; the fixture supplies actual native crypto output.</summary>
    private static BrowserSessionIdentity? Validate(BrowserSessionToken token) =>
        BrowserSessionIdentity.Validate(token.Principal, token.Encoded, BrowserSessionToken.Intent, token.Now);

    /// <summary>Changes only the supplied source expiry relative to the independently encoded issuance time.</summary>
    private static string ChangeDeadline(string payload, int seconds)
    {
        return ChangeDate(payload, BrowserSessionToken.SourceExpiryClaim, seconds);
    }

    /// <summary>Preserves every other field while changing only a selected date relative to issuance.</summary>
    private static string ChangeDate(string payload, string name, int seconds)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        var issued = DateTimeOffset.FromUnixTimeSeconds(document.RootElement.GetProperty(BrowserSessionToken.IssuedAtClaim).GetInt64());
        return BrowserSessionToken.Field(payload, name, BrowserSessionToken.Epoch(issued.AddSeconds(seconds)));
    }

    /// <summary>Uses an otherwise valid value so shape failures cannot pass because of unrelated lifetime rejection.</summary>
    private static string InvalidShape(string payload, string name, string? raw)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        return BrowserSessionToken.Field(payload, name, raw?.Replace(ValuePlaceholder, document.RootElement.GetProperty(name).GetRawText(), StringComparison.Ordinal));
    }

    /// <summary>Retains duplicate raw JSON fields which normal token descriptor dictionaries cannot represent.</summary>
    private static string DuplicateDate(string payload, string name)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        return payload[..^1] + ',' + System.Text.Json.JsonSerializer.Serialize(name) + ':' + document.RootElement.GetProperty(name).GetRawText() + '}';
    }

    /// <summary>Exercises the existing real B1 codec and policy without pretending an in-memory projection is a database commit.</summary>
    private static void RequireProtectedTicket(BrowserSessionIdentity binding, AuthenticationTicket ticket, DateTimeOffset now)
    {
        var row = SessionTicketPolicy.Create(new string('A', DigestLength), BrowserSessionToken.Subject, BrowserSessionToken.Session,
            now, binding.SourceExpiresAt, TimeSpan.FromMinutes(IdleMinutes), TimeSpan.FromHours(SourceLifetimeHours));
        var codec = new SessionTicketCodec(new EphemeralDataProtectionProvider());
        row.ProtectedTicket = codec.Protect(row, ticket);
        var restored = Assert.IsType<AuthenticationTicket>(codec.Unprotect(row));
        Assert.Equal(now.AddMinutes(IdleMinutes), restored.Properties.ExpiresUtc);
        Assert.Equal(binding.SourceExpiresAt.UtcTicks, row.AbsoluteExpiresAt);
    }
}
