using System.Text;
using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Checks explicit new identity/credential intent and strict parsing without accepting source secrets or caller-selected permissions.</summary>
public sealed class ApplicationCloneTests
{
    private const string Client = "new-client";
    private const string Display = "New client";
    private const string PaddedClient = " new-client";
    private const string Wildcard = "*";
    private const int SourceRevision = 4;
    private const string ValidJson = """{"confirmed":true,"clientId":"new-client","displayName":"New client","credentialExpiresAt":"2030-01-02T00:00:00Z"}""";
    private const string DuplicateJson = """{"confirmed":false,"confirmed":true,"clientId":"new-client","displayName":"New","credentialExpiresAt":"2030-01-02T00:00:00Z"}""";
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Source = "source-application";
    private const string Future = "2030-01-02T00:00:00Z";

    /// <summary>One confirmed clone binds the source precondition and creates a distinct immutable destination intent.</summary>
    [Fact]
    public void Given_ConfirmedNewIdentity_When_CloneValidated_Then_SourceVersionAndNewExpiryRemainBound()
    {
        var version = ApplicationVersion.Create(Source, SourceRevision);
        var command = ApplicationMutationCommand.Clone(Source, version, new(true, Client, Display, Future), Now);
        Assert.NotNull(command);
        Assert.Equal(Source, command.ApplicationId);
        Assert.Equal(version, command.Version);
        Assert.Equal(Client, command.ClientId);
        Assert.Equal(Display, command.DisplayName);
        Assert.Equal(Now.AddDays(1), command.CredentialExpiry);
        Assert.Empty(command.Redirects);
        Assert.Empty(command.Scopes);
        var parsed = ApplicationJson.Parse<ApplicationClone>(Encoding.UTF8.GetBytes(ValidJson));
        Assert.True(parsed.Confirmed);
        Assert.Equal(command.ClientId, parsed.ClientId);
    }

    /// <summary>Unconfirmed, invalid identity, stale expiry and malformed preconditions never become executable clone intent.</summary>
    [Fact]
    public void Given_InvalidCloneIntent_When_Validated_Then_NoCommandIsCreated()
    {
        var version = ApplicationVersion.Create(Source, SourceRevision);
        var input = new ApplicationClone(true, Client, Display, Future);
        Assert.Null(ApplicationMutationCommand.Clone(Source, version, input with
        {
            Confirmed = false
        }, Now));
        Assert.Null(ApplicationMutationCommand.Clone(Source, version, input with
        {
            ClientId = PaddedClient
        }, Now));
        Assert.Null(ApplicationMutationCommand.Clone(Source, version, input with
        {
            DisplayName = ""
        }, Now));
        Assert.Null(ApplicationMutationCommand.Clone(Source, version, input, Now.AddDays(1)));
        Assert.Null(ApplicationMutationCommand.Clone(Source, Wildcard, input, Now));
    }

    /// <summary>Unknown credential/configuration properties and duplicate/missing confirmation cannot be hidden in valid-looking JSON.</summary>
    [Theory]
    [InlineData("""{"confirmed":true,"clientId":"new-client","displayName":"New","credentialExpiresAt":"2030-01-02T00:00:00Z","clientSecret":"untrusted"}""")]
    [InlineData("""{"confirmed":true,"clientId":"new-client","displayName":"New","credentialExpiresAt":"2030-01-02T00:00:00Z","scopes":[]}""")]
    [InlineData("""{"clientId":"new-client","displayName":"New","credentialExpiresAt":"2030-01-02T00:00:00Z"}""")]
    public void Given_AmbiguousOrExtraCloneFields_When_Parsed_Then_RequestIsRejected(string json) =>
        Assert.Throws<JsonException>(() => ApplicationJson.Parse<ApplicationClone>(Encoding.UTF8.GetBytes(json)));
    /// <summary>Duplicate confirmation is rejected before deserialization can choose the final value.</summary>
    [Fact]
    public void Given_DuplicateConfirmation_When_CloneJsonParsed_Then_AmbiguityIsRejected() =>
        Assert.Throws<InvalidDataException>(() => ApplicationJson.Parse<ApplicationClone>(Encoding.UTF8.GetBytes(DuplicateJson)));
}
