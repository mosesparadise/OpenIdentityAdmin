using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Separates native end-session validation from future fixed-client policy, confirmation and committed issuer revocation.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeEndSessionTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly string[] InvalidDestinations =
    [
        NativeEndSessionFixture.ReturnUri + "/", NativeEndSessionFixture.ReturnUri + "?next=elsewhere",
        NativeEndSessionFixture.ReturnUri.Replace("/logout/", "/Logout/", StringComparison.Ordinal),
        NativeEndSessionFixture.ReturnUri.Replace(".test/", ".test:7443/", StringComparison.Ordinal)
    ];
    private const string LogoutHint = "logout_hint";
    private const string ForeignSession = "untrusted-foreign-session-selector";
    private const string UnknownClient = "unregistered-end-session-client";

    /// <summary>Neither a genuine nor a forged public selector confers revocation authority during native validation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RegisteredWebClient_When_NoTokenHintIsSupplied_Then_NativeValidationDoesNotRevokeTheIssuerSession(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await NativeEndSessionFixture.ConfigureAsync(scenario, OpenIddictConstants.Permissions.Endpoints.EndSession);
        var before = await scenario.Account.SnapshotAsync();
        var tokens = await NativeAccountPersistence.CountAsync(scenario);
        foreach (var hint in new string?[] { null, scenario.Account.Session.SessionId, ForeignSession })
        {
            var request = NativeEndSessionFixture.Request();
            request[LogoutHint] = hint;
            var result = await NativeEndSessionFixture.ValidateAsync(scenario, request);
            NativeEndSessionFixture.RequireAccepted(result);
            Assert.True((string?)result.Request[LogoutHint] == hint);
            Assert.True(before == await scenario.Account.SnapshotAsync());
            Assert.Equal(tokens, await NativeAccountPersistence.CountAsync(scenario));
        }
    }

    /// <summary>Only the exact registered Web return URI passes; each changed request is evaluated against fresh persisted metadata.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_RegisteredReturnUri_When_ClientOrDestinationChanges_Then_NativeValidationRejectsTheRequest(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await NativeEndSessionFixture.ConfigureAsync(scenario, OpenIddictConstants.Permissions.Endpoints.EndSession);
        NativeEndSessionFixture.RequireAccepted(await NativeEndSessionFixture.ValidateAsync(scenario, NativeEndSessionFixture.Request()));
        var unknown = NativeEndSessionFixture.Request();
        unknown.ClientId = UnknownClient;
        var invalidClient = await NativeEndSessionFixture.ValidateAsync(scenario, unknown);
        Assert.True(invalidClient.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, invalidClient.Error);
        foreach (var destination in InvalidDestinations)
        {
            var request = NativeEndSessionFixture.Request();
            request.PostLogoutRedirectUri = destination;
            var result = await NativeEndSessionFixture.ValidateAsync(scenario, request);
            Assert.True(result.IsRejected);
            Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, result.Error);
        }
        NativeEndSessionFixture.RequireAccepted(await NativeEndSessionFixture.ValidateAsync(scenario, NativeEndSessionFixture.Request()));
    }

    /// <summary>Characterizes native compatibility explicitly; future product policy must require the fixed client and exact new permission.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_PersistedEndpointPermission_When_MetadataChanges_Then_NativeCompatibilityDoesNotDefineTheProductProfile(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await NativeEndSessionFixture.ConfigureAsync(scenario, OpenIddictConstants.Permissions.Endpoints.EndSession);
        var inferred = NativeEndSessionFixture.Request();
        inferred.ClientId = null;
        var inferredResult = await NativeEndSessionFixture.ValidateAsync(scenario, inferred);
        NativeEndSessionFixture.RequireAccepted(inferredResult);
        Assert.Null(inferredResult.ClientId);
        await NativeEndSessionFixture.ConfigureAsync(scenario, null);
        var denied = await NativeEndSessionFixture.ValidateAsync(scenario, NativeEndSessionFixture.Request());
        Assert.True(denied.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.UnauthorizedClient, denied.Error);
        await NativeEndSessionFixture.ConfigureAsync(scenario, NativeEndSessionFixture.LegacyPermission);
        NativeEndSessionFixture.RequireAccepted(await NativeEndSessionFixture.ValidateAsync(scenario, NativeEndSessionFixture.Request()));
        await NativeEndSessionFixture.ConfigureAsync(scenario, OpenIddictConstants.Permissions.Endpoints.EndSession);
        NativeEndSessionFixture.RequireAccepted(await NativeEndSessionFixture.ValidateAsync(scenario, NativeEndSessionFixture.Request()));
    }

    /// <summary>Requires independently discovered cases on both installed relational providers.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers =>
        ProviderNames.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));
}
