using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Checks independently specified bounds and lossless values before either native provider sees a mutation.</summary>
public sealed class ApplicationEditorTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] InvalidFields = ["clientId", "credentialExpiresAt", "displayName", "redirectUris"];
    private static readonly string[] InvalidMessages = ["The value is missing, unsupported or outside its allowed bounds."];
    private const string DecomposedWithEmoji = "e\u0301😀";
    private const char HighSurrogate = (char)0xd800;
    private const char LowSurrogate = (char)0xdc00;
    private const string ControlName = "name\tvalue";
    private const string InvalidInput = "secret invalid";
    private const string NullCharacter = "\0";
    private const string DisplayName = "Example";
    private const string CallbackQuery = "?state=1";
    private const string Future = "2030-01-01T00:00:01Z";
    private const string Callback = "https://client.example/callback";
    private const int IdentifierLimit = 100;
    private const int DisplayLimit = 200;
    private const int RedirectLimit = 2048;
    private const int CallbackLimit = 10;

    /// <summary>Valid exact Unicode identifiers and bounded callbacks remain unchanged rather than being normalized.</summary>
    [Fact]
    public void Given_ExactMaximumValues_When_CreateValidated_Then_ValuesRemainUnchanged()
    {
        var clientId = new string('é', IdentifierLimit);
        var display = new string('d', DisplayLimit);
        var redirects = Enumerable.Range(0, CallbackLimit).Select(index => Callback + index).ToArray();
        var input = new ApplicationCreate(clientId, display, redirects, Future);
        Assert.Empty(ApplicationEditorRules.Validate(input, Now));
        Assert.Same(clientId, input.ClientId);
        Assert.Same(redirects, input.RedirectUris);
        Assert.True(ApplicationEditorRules.IsClientId(DecomposedWithEmoji));
        Assert.True(ApplicationEditorRules.IsRedirect(Callback + new string('x', RedirectLimit - Callback.Length)));
        Assert.False(ApplicationEditorRules.IsRedirect(Callback + new string('x', RedirectLimit - Callback.Length + 1)));
    }

    /// <summary>Padding aliases, whitespace, controls and invalid UTF-16 cannot produce native identifier aliases.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("client ")]
    [InlineData(" client")]
    [InlineData("client\u00a0id")]
    [InlineData("client\n")]
    [InlineData("client\0")]
    public void Given_AmbiguousIdentifier_When_Validated_Then_ItIsRejected(string? value) =>
        Assert.False(ApplicationEditorRules.IsClientId(value));

    /// <summary>Surrogate errors are constructed at runtime so discovery serialization cannot repair the negative input.</summary>
    [Fact]
    public void Given_InvalidUnicodeOrOverlongText_When_Validated_Then_ItIsRejected()
    {
        var high = new string(HighSurrogate, 1);
        var low = new string(LowSurrogate, 1);
        Assert.False(ApplicationEditorRules.IsClientId(high));
        Assert.False(ApplicationEditorRules.IsClientId(low));
        Assert.False(ApplicationEditorRules.IsClientId(new string('x', IdentifierLimit + 1)));
        Assert.False(ApplicationEditorRules.IsText('a' + high + 'b', DisplayLimit));
        Assert.False(ApplicationEditorRules.IsText(new string('d', DisplayLimit + 1), DisplayLimit));
        Assert.False(ApplicationEditorRules.IsText(ControlName, DisplayLimit));
    }

    /// <summary>Callbacks must already have their exact canonical HTTPS representation without credentials or fragments.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("/callback")]
    [InlineData("http://client.example/callback")]
    [InlineData("https://user:password@client.example/callback")]
    [InlineData("https://client.example/callback#fragment")]
    [InlineData("https://CLIENT.example/callback")]
    [InlineData("https://client.example:443/callback")]
    [InlineData("https://client.example")]
    [InlineData("https://client.example/a/../callback")]
    [InlineData("https://client.example/call back")]
    [InlineData("https://client.example/callback\n")]
    [InlineData(" https://client.example/callback")]
    public void Given_UnsafeOrNoncanonicalCallback_When_Validated_Then_ItIsRejected(string? value) =>
        Assert.False(ApplicationEditorRules.IsRedirect(value));

    /// <summary>Malformed collections and field values produce only fixed field keys and messages, never reflected data.</summary>
    [Fact]
    public void Given_InvalidFields_When_Validated_Then_OnlySafeFieldErrorsAreReturned()
    {
        var input = new ApplicationCreate(InvalidInput, NullCharacter, [Callback, Callback], InvalidInput);
        var errors = ApplicationEditorRules.Validate(input, Now);
        Assert.Equal(InvalidFields, errors.Keys.Order());
        Assert.All(errors.Values, messages => Assert.Equal(InvalidMessages, messages));
        Assert.Contains("redirectUris", ApplicationEditorRules.Validate(new ApplicationEdit(DisplayName, [])).Keys);
        Assert.Contains("redirectUris", ApplicationEditorRules.Validate(new ApplicationEdit(DisplayName, null!)).Keys);
        Assert.Contains("redirectUris", ApplicationEditorRules.Validate(new ApplicationEdit(DisplayName, [null!])).Keys);
        Assert.Contains("redirectUris", ApplicationEditorRules.Validate(new ApplicationEdit(DisplayName,
            Enumerable.Range(0, CallbackLimit + 1).Select(index => Callback + index).ToArray())).Keys);
        Assert.Empty(ApplicationEditorRules.Validate(new ApplicationEdit(DisplayName, [Callback, Callback + CallbackQuery])));
    }

    /// <summary>Expiry rejects alternative encodings, invalid dates, equality and elapsed deadlines.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2029-12-31T23:59:59Z")]
    [InlineData("2030-01-01T00:00:00Z")]
    [InlineData("2030-01-01T00:00:01+00:00")]
    [InlineData("2030-01-01T01:00:01+01:00")]
    [InlineData("2030-01-01T00:00:01.000Z")]
    [InlineData("2030-01-01t00:00:01z")]
    [InlineData("2030-1-1T00:00:01Z")]
    [InlineData("2030-02-30T00:00:01Z")]
    [InlineData("2030-01-01T00:00:01Z ")]
    public void Given_NonfutureOrNoncanonicalExpiry_When_Parsed_Then_ItIsRejected(string? value) =>
        Assert.False(ApplicationEditorRules.TryReadExpiry(value, Now, out _));

    /// <summary>Replacement needs explicit confirmation and the future boundary is evaluated at the supplied clock.</summary>
    [Fact]
    public void Given_ExplicitReplacement_When_Validated_Then_ConfirmationAndCurrentExpiryAreBothRequired()
    {
        Assert.True(ApplicationEditorRules.TryReadExpiry(Future, Now, out var expiry));
        Assert.Equal(Now.AddSeconds(1), expiry);
        Assert.Empty(ApplicationEditorRules.Validate(new ApplicationSecretReplacement(true, Future), Now));
        Assert.Contains("confirmed", ApplicationEditorRules.Validate(new ApplicationSecretReplacement(false, Future), Now).Keys);
        Assert.Contains("credentialExpiresAt", ApplicationEditorRules.Validate(new ApplicationSecretReplacement(true, Future), expiry).Keys);
        Assert.False(ApplicationEditorRules.TryReadExpiry(Future, expiry.AddTicks(1), out _));
    }
}
