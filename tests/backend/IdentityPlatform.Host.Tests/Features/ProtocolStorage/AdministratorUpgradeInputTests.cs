using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Requires an exact independent upgrade precondition while preserving initializer input isolation.</summary>
public sealed class AdministratorUpgradeInputTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string OperationId = "operationId";
    private const string OperatorReference = "operatorReference";
    private const string RedirectUri = "redirectUri";
    private const string PublicKey = "publicKey";
    private const string PrivateExponent = "d";
    private const string PrivateValue = "AQAB";
    private const string UnexpectedField = "targetProfile";
    private const string BrowserProfile = "administrator-browser-v1";
    private const string InvalidOperator = "unsafe operator";
    private const string Collision = "https://upgrade-input.example.test/bff/logout/return";
    private const string Callback = "https://upgrade-input.example.test/callback";
    private const string Operator = "upgrade-operator";
    private const string Revision = "expectedRevision";
    private const string Invalid = "Invalid administrator protocol initialization input.";
    private const string Duplicate = "{\"expectedRevision\":1,";
    private const int MaximumBytes = 12288;

    /// <summary>Boundary revisions remain exact while the immutable original policy is retained for precondition verification.</summary>
    [Theory]
    [InlineData(1L)]
    [InlineData(long.MaxValue - 1)]
    public async Task Given_CanonicalRevision_When_UpgradeInputIsRead_Then_ExactOriginalPolicyAndRevisionAreRetained(long revision)
    {
        var json = Document();
        json[Revision] = revision;
        using var stream = Bytes(json.ToJsonString());
        var input = await AdministratorRegistrationInput.ReadUpgradeAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(revision, input.ExpectedRevision);
        Assert.Equal(Callback, input.Policy.RedirectUri);
        Assert.Equal(fixture.Key.Fingerprint, input.Policy.Key.Fingerprint);
        Assert.Empty(input.Policy.Application().PostLogoutRedirectUris);
        Assert.NotNull(input.Policy.ForBrowserSessions());
    }

    /// <summary>Coerced, noncanonical, missing or overflow revisions never reach native registration ownership.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("1e0")]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("9223372036854775807")]
    [InlineData("9223372036854775808")]
    public async Task Given_InvalidRevision_When_UpgradeInputIsRead_Then_TheSafeParserRejects(string value)
    {
        var json = Document();
        json[Revision] = JsonNode.Parse(value);
        await RejectAsync(Encoding.UTF8.GetBytes(json.ToJsonString()));
    }

    /// <summary>The shared decoder still rejects private/ambiguous/oversized input and callbacks colliding with the fixed logout route.</summary>
    [Theory]
    [InlineData(UpgradeInputDefect.MissingRevision)]
    [InlineData(UpgradeInputDefect.DuplicateRevision)]
    [InlineData(UpgradeInputDefect.PrivateKey)]
    [InlineData(UpgradeInputDefect.UnknownField)]
    [InlineData(UpgradeInputDefect.EmptyOperation)]
    [InlineData(UpgradeInputDefect.BadOperator)]
    [InlineData(UpgradeInputDefect.Collision)]
    [InlineData(UpgradeInputDefect.InvalidUtf8)]
    [InlineData(UpgradeInputDefect.Oversized)]
    public async Task Given_InvalidUpgradeDocument_When_Read_Then_NoSubmittedContentEscapes(UpgradeInputDefect defect)
    {
        var json = Document();
        switch (defect)
        {
            case UpgradeInputDefect.MissingRevision:
                json.Remove(Revision);
                break;
            case UpgradeInputDefect.PrivateKey:
                json[PublicKey]![PrivateExponent] = PrivateValue;
                break;
            case UpgradeInputDefect.UnknownField:
                json[UnexpectedField] = BrowserProfile;
                break;
            case UpgradeInputDefect.EmptyOperation:
                json[OperationId] = Guid.Empty.ToString();
                break;
            case UpgradeInputDefect.BadOperator:
                json[OperatorReference] = InvalidOperator;
                break;
            case UpgradeInputDefect.Collision:
                json[RedirectUri] = Collision;
                break;
        }
        var document = json.ToJsonString();
        await RejectAsync(defect switch
        {
            UpgradeInputDefect.DuplicateRevision => Encoding.UTF8.GetBytes(Duplicate + document[1..]),
            UpgradeInputDefect.InvalidUtf8 => [byte.MaxValue],
            UpgradeInputDefect.Oversized => new byte[MaximumBytes + 1],
            _ => Encoding.UTF8.GetBytes(document)
        });
    }

    /// <summary>An upgrade document cannot silently select upgrade semantics through the existing initialization parser.</summary>
    [Fact]
    public async Task Given_UpgradeFields_When_InitializerReadsThem_Then_InitializationRejects()
    {
        using var stream = Bytes(Document().ToJsonString());
        await Assert.ThrowsAsync<ArgumentException>(() => AdministratorRegistrationInput.ReadAsync(stream, TestContext.Current.CancellationToken));
    }

    /// <summary>Creates public-only synthetic metadata with a separately fixed expected original revision.</summary>
    private JsonObject Document() => new()
    {
        [OperationId] = Guid.NewGuid().ToString(),
        [OperatorReference] = Operator,
        [RedirectUri] = Callback,
        [PublicKey] = JsonNode.Parse(fixture.Json),
        [Revision] = 1
    };

    /// <summary>Asserts the finite safe exception rather than retaining parser details or submitted input.</summary>
    private static async Task RejectAsync(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AdministratorRegistrationInput.ReadUpgradeAsync(stream, TestContext.Current.CancellationToken));
        Assert.Equal(Invalid, error.Message);
        Assert.Null(error.InnerException);
    }

    /// <summary>Encodes a small synthetic document through the same actual stream boundary as the command.</summary>
    private static MemoryStream Bytes(string json) => new(Encoding.UTF8.GetBytes(json));
}

/// <summary>Names independent input-shape and secrecy failures for the explicit browser upgrade.</summary>
public enum UpgradeInputDefect
{
    MissingRevision, DuplicateRevision, PrivateKey, UnknownField, EmptyOperation, BadOperator, Collision, InvalidUtf8, Oversized
}
