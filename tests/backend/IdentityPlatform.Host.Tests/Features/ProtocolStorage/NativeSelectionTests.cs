using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises actual native extraction before immutable manager selection and rejects later profile substitution.</summary>
public sealed class NativeSelectionTests
{
    private const int OriginalRequestSeconds = 10;
    private const string PilotQuery = "client_id=identityplatform-pilot";

    /// <summary>The selection fixture advances both elapsed checks and timer cancellation on the original ten-second boundary.</summary>
    [Fact]
    public async Task Given_OriginalDeadlineReached_When_NativeExtractionContinues_Then_NoSelectionResultCanSucceed()
    {
        await using var probe = new NativeSelectionProbe();
        probe.BeforeManager = () => probe.Storage.Clock.Advance(TimeSpan.FromSeconds(OriginalRequestSeconds));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.RunAsync(false, PilotQuery));
    }

    /// <summary>Only an exact scalar pilot identity extracted on an approved endpoint selects legacy native stores.</summary>
    [Theory]
    [InlineData(false, "client_id=identityplatform-pilot", null, false)]
    [InlineData(true, "client_id=identityplatform-pilot", null, false)]
    [InlineData(true, "grant_type=authorization_code", "identityplatform-pilot", false)]
    [InlineData(true, "client_id=identityplatform-pilot", "identityplatform-pilot", false)]
    [InlineData(false, "client_id=identity-admin-bff", null, true)]
    [InlineData(true, "client_id=identity-admin-bff", null, true)]
    [InlineData(false, "client_id=IDENTITYPLATFORM-PILOT", null, true)]
    [InlineData(false, "response_type=code", null, true)]
    [InlineData(false, "client_id=identityplatform-pilot&client_id=identityplatform-pilot", null, true)]
    public async Task Given_ExtractedClient_When_FirstNativeManagerResolves_Then_ExactProfileSelectsStore(bool token, string values, string? basic, bool owned)
    {
        await using var probe = new NativeSelectionProbe();
        Assert.True(await probe.RunAsync(token, values, basic));
        Assert.Null(probe.Error);
        Assert.Equal(owned, probe.Owned);
    }

    /// <summary>The existing pre/post Basic consistency guard rejects substitution before store selection or manager resolution.</summary>
    [Theory]
    [InlineData("client_id=identity-admin-bff")]
    [InlineData("client_id=identityplatform-pilot&client_id=identityplatform-pilot")]
    public async Task Given_ConflictingBodyAndBasic_When_NativeExtractionRuns_Then_SelectionNeverOccurs(string values)
    {
        await using var probe = new NativeSelectionProbe();
        Assert.True(await probe.RunAsync(true, values, PilotProfile.ClientId));
        Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, probe.Error);
        Assert.Null(probe.Owned);
    }

    /// <summary>A changed resolved client cannot persist using the initial extracted client's selected store.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ProfileChangesAfterSelection_When_SignInStarts_Then_FenceRejects(bool pilot)
    {
        await using var probe = new StorageRequestProbe(services => services.AddProtocolStorageSelection());
        await using var scope = probe.Root.CreateAsyncScope();
        var selection = scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>();
        selection.Select(pilot ? ProtocolStorageMode.LegacyPilot : ProtocolStorageMode.Owned);
        var context = new OpenIddictServerEvents.ProcessSignInContext(new OpenIddictServerTransaction
        {
            EndpointType = OpenIddictServerEndpointType.Authorization,
            Request = new OpenIddictRequest { ClientId = pilot ? AdministratorProtocolIdentity.ClientId : PilotProfile.ClientId }
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ServerStoreFence(selection).HandleAsync(context).AsTask());
    }

    /// <summary>Unsupported fixed-profile ID-token hints fail before the native status handler can resolve or mutate any store.</summary>
    [Fact]
    public async Task Given_AdministratorIdentityTokenHint_When_EntryValidationRuns_Then_NoStoreCanBeTouched()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken)
            .SetAudiences(AdministratorProtocolIdentity.ClientId);
        var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction { Logger = NullLogger.Instance })
        {
            Principal = principal
        };
        await new DurableServerValidation(null!, null!).HandleAsync(context);
        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
    }

    /// <summary>Protected administrator credentials cannot trigger replay revocation under a different requesting profile.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("identityplatform-pilot")]
    [InlineData("IDENTITY-ADMIN-BFF")]
    public async Task Given_AdministratorCodeWithWrongClient_When_EntryValidationRuns_Then_NoStoreCanBeTouched(string? client)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode)
            .SetPresenters(AdministratorProtocolIdentity.ClientId);
        var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction
        {
            Logger = NullLogger.Instance,
            Request = new OpenIddictRequest { ClientId = client }
        })
        {
            Principal = principal
        };
        await new DurableServerValidation(null!, null!).HandleAsync(context);
        Assert.True(context.IsRejected);
        Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
    }
}
