using System.Text;
using System.Text.Json;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using IdentityPlatform.Migrator.Features.Operations;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks explicit public-only initialization input and mutually exclusive operation selection before native database work.</summary>
public sealed class AdministratorRegistrationInputTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Callback = "https://registration-input.example.test/callback";
    private const string Operator = "test-operator";
    private const string OperationSetting = "Operation";
    private const string PublicKeyPrefix = "\"publicKey\":";
    private const string DuplicatePrefix = "{\"operatorReference\":\"another\",";
    private const string PrivatePrefix = "\"publicKey\":{\"d\":\"AQAB\",";
    private const string ObjectStart = "{";
    private const string GuidFormat = "D";
    private const int MaximumBytes = 12288;

    /// <summary>A valid bounded document produces the exact immutable callback and public key binding.</summary>
    [Fact]
    public async Task Given_PublicInitializationInput_When_Read_Then_ExactPolicyIsRetained()
    {
        var id = Guid.NewGuid();
        using var input = Stream(Document(id));
        var result = await AdministratorRegistrationInput.ReadAsync(input, TestContext.Current.CancellationToken);
        Assert.Equal(id, result.OperationId);
        Assert.Equal(Operator, result.OperatorReference);
        Assert.Equal(Callback, result.Policy.RedirectUri);
        Assert.Equal(fixture.Key.Fingerprint, result.Policy.Key.Fingerprint);
    }

    /// <summary>Ambiguity, private key fields, invalid encoding and input over the bound fail before initialization with a fixed safe error.</summary>
    [Theory]
    [InlineData(InputDefect.Duplicate)]
    [InlineData(InputDefect.PrivateKey)]
    [InlineData(InputDefect.EmptyOperation)]
    [InlineData(InputDefect.InvalidUtf8)]
    [InlineData(InputDefect.Oversized)]
    public async Task Given_InvalidInput_When_Read_Then_NoParsedContentEscapes(InputDefect defect)
    {
        var json = Document(defect == InputDefect.EmptyOperation ? Guid.Empty : Guid.NewGuid());
        byte[] bytes = defect switch
        {
            InputDefect.Duplicate => Encoding.UTF8.GetBytes(DuplicatePrefix + json[1..]),
            InputDefect.PrivateKey => Encoding.UTF8.GetBytes(json.Replace(PublicKeyPrefix + ObjectStart, PrivatePrefix, StringComparison.Ordinal)),
            InputDefect.InvalidUtf8 => [byte.MaxValue],
            InputDefect.Oversized => new byte[MaximumBytes + 1],
            _ => Encoding.UTF8.GetBytes(json)
        };
        using var input = new MemoryStream(bytes);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => AdministratorRegistrationInput.ReadAsync(input, TestContext.Current.CancellationToken));
        Assert.Null(error.InnerException);
        Assert.Equal("Invalid administrator protocol initialization input.", error.Message);
    }

    /// <summary>Protocol initialization is a separately explicit selection, never an implicit consequence of schema upgrade.</summary>
    [Theory]
    [InlineData("upgrade-administrator-browser-registration", 3)]
    [InlineData(null, 0)]
    [InlineData("upgrade", 0)]
    [InlineData("initialize-administrator", 1)]
    [InlineData("initialize-administrator-protocol", 2)]
    public void Given_OperationSelection_When_Parsed_Then_OnlyExactOperationIsChosen(string? operation, int expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [OperationSetting] = operation }).Build();
        Assert.Equal((MigratorOperation)expected, configuration.SelectedOperation());
    }

    /// <summary>Builds synthetic public input without secrets or a database connection.</summary>
    private string Document(Guid id)
    {
        using var key = JsonDocument.Parse(fixture.Json);
        return JsonSerializer.Serialize(new
        {
            operationId = id.ToString(GuidFormat),
            operatorReference = Operator,
            redirectUri = Callback,
            publicKey = key.RootElement
        });
    }

    /// <summary>Supplies original UTF-8 bytes to the bounded parser.</summary>
    private static MemoryStream Stream(string json) => new(Encoding.UTF8.GetBytes(json));
}

/// <summary>Names independently seeded public initialization input defects.</summary>
public enum InputDefect
{
    Duplicate, PrivateKey, EmptyOperation, InvalidUtf8, Oversized
}
