using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves strict raw shape checks operate on the same genuinely signature-validated compact token.</summary>
public sealed class ClientAssertionShapeTests(AssertionSigningFixture fixture) : IClassFixture<AssertionSigningFixture>
{
    private const string Audience = "\"aud\":\"https://assertion.example.test/\"";
    private const string IssuedAt = "\"iat\":100";
    private const string PayloadEnd = "\"jti\":\"assertion-proof-01\"";
    private const string WrongKey = "other-assertion-key";
    private const long Now = 100;
    private const string HeaderKey = "\"kid\":\"assertion-test-key\"";
    private const string AliasedNotBefore = ",\"NBF\":101";
    private const string MemberSeparator = ",";

    /// <summary>The fixed native-compatible assertion shape accepts only a successful signature result for those exact serialized bytes.</summary>
    [Fact]
    public async Task Given_ValidSignatureAndFixedShape_When_PostValidationRuns_Then_OnlyTheSameTokenCanYieldReplayInputs()
    {
        var context = await fixture.ContextAsync();
        Assert.True(context.TokenValidationResult!.IsValid);
        Assert.True(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId)!.Allows(Now));
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, WrongKey));
        context.Token += context.Token;
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId));
    }

    /// <summary>Scalar raw issue time cannot be absent, stringified, fractional or duplicated even after real signature verification.</summary>
    [Theory]
    [InlineData("\"iat\":\"100\"")]
    [InlineData("\"iat\":100.5")]
    [InlineData("\"iat\":100,\"iat\":100")]
    [InlineData("\"iat\":100,\"IAT\":100")]
    [InlineData("\"other\":100")]
    public async Task Given_NoncanonicalIssueTime_When_PostValidationRuns_Then_NoReplayInputIsCreated(string replacement)
    {
        var context = await fixture.ContextAsync(AssertionSigningFixture.Payload.Replace(IssuedAt, replacement, StringComparison.Ordinal));
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId));
    }

    /// <summary>Endpoint audiences, duplicate audience arrays and optional-claim aliases cannot widen the fixed assertion contract.</summary>
    [Theory]
    [InlineData("\"aud\":\"https://assertion.example.test/connect/token\"")]
    [InlineData("\"aud\":[\"https://assertion.example.test/\",\"https://assertion.example.test/\"]")]
    public async Task Given_NoncanonicalAudience_When_PostValidationRuns_Then_CanonicalIssuerIsRequired(string replacement)
    {
        var context = await fixture.ContextAsync(AssertionSigningFixture.Payload.Replace(Audience, replacement, StringComparison.Ordinal));
        Assert.True(context.TokenValidationResult!.IsValid);
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId));
    }

    /// <summary>An optional not-before alias is rejected rather than ignored as an absent bound.</summary>
    [Fact]
    public async Task Given_AliasedOptionalNotBefore_When_PostValidationRuns_Then_TheClaimCannotDisappear()
    {
        var context = await fixture.ContextAsync(AssertionSigningFixture.Payload.Replace(PayloadEnd, PayloadEnd + AliasedNotBefore, StringComparison.Ordinal));
        Assert.True(context.TokenValidationResult!.IsValid);
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId));
    }

    /// <summary>Valid fallback-key verification cannot substitute a differently identified registered key for the fixed assertion key.</summary>
    [Fact]
    public async Task Given_FallbackSigningKey_When_CryptographyAccepts_Then_ActualValidatedKeyIdentityMustStillMatch()
    {
        var context = await fixture.ContextAsync(validationKeyId: WrongKey);
        Assert.True(context.TokenValidationResult!.IsValid);
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId));
    }

    /// <summary>Cryptographically valid assertions still cannot carry remote, embedded or critical fallback key metadata.</summary>
    [Theory]
    [InlineData("\"jku\":\"https://key.example.test/jwks\"")]
    [InlineData("\"jwk\":{}")]
    [InlineData("\"crit\":[\"extra\"]")]
    public async Task Given_ForbiddenHeader_When_PostValidationRuns_Then_NoFallbackMetadataIsAccepted(string header)
    {
        var context = await fixture.ContextAsync(header: AssertionSigningFixture.Header.Replace(HeaderKey, HeaderKey + MemberSeparator + header, StringComparison.Ordinal));
        Assert.True(context.TokenValidationResult!.IsValid);
        Assert.Null(ClientAssertionShape.Read(context, AssertionSigningFixture.Issuer, AssertionSigningFixture.KeyId));
    }
}
