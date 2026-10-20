using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves actual HTTP challenges suppress publication after state-creation commit observation or child cleanup fails.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientCreationTests(BootstrapFixture fixture)
{
    private const int FailedCreationStates = 2;
    private const int RecoveredStates = 3;
    private const int BaselineRedeemed = 1;
    private const int RecoveredRedeemed = 2;

    /// <summary>Brackets one injected post-commit fault with genuine native exchanges and reconciles the retained state through a fresh root.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_StateCommitted_When_CommitObservationOrChildCleanupFails_Then_NoChallengeIsPublishedAndFreshRootReconcilesWithoutRetry(
        string provider, bool cleanup)
    {
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(keys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        var fault = new NativeClientCreationFault(cleanup);
        await using var client = new NativeClientHttp(database, issuer, profile, keys, fault.Configure);
        await ExchangeAsync(client, issuer);
        await RequireFailureAsync(client, issuer, fault);
        using var recoveredKeys = NativeClientInteroperabilityTests.Load(material);
        await using var recovered = new NativeClientHttp(database, issuer, profile, recoveredKeys);
        await NativeClientInteroperabilityTests.RequireStatesAsync(recovered, FailedCreationStates, BaselineRedeemed);
        await RequireRetainedAsync(recovered, profile, fault);
        await ExchangeAsync(recovered, issuer);
        await NativeClientInteroperabilityTests.RequireStatesAsync(recovered, RecoveredStates, RecoveredRedeemed);
        await RequireRetainedAsync(recovered, profile, fault);
        Assert.Equal(1, client.Bridge.ParSends);
        Assert.Equal(1, client.Bridge.TokenSends);
        Assert.Equal(1, recovered.Bridge.ParSends);
        Assert.Equal(1, recovered.Bridge.TokenSends);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Faults);
        Assert.Equal(1, fault.Disposals);
    }

    /// <summary>Requires the specific injected failure and unchanged issuer credentials/sends, and disarms injection even when an assertion fails.</summary>
    private static async Task RequireFailureAsync(NativeClientHttp client, AdministratorProtocolScenario issuer, NativeClientCreationFault fault)
    {
        var before = await NativeAccountPersistence.CountAsync(issuer);
        fault.Armed = true;
        try
        {
            var result = await client.InvokeAsync();
            Assert.True(fault.Contains(result.Failure), result.Failure?.GetType().FullName);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.Status);
            Assert.False(result.Authentication?.Succeeded == true);
            Assert.Equal(0, result.BodyLength);
            Assert.True(result.Location.Length == 0);
            Assert.True(result.Cookies.Length == 0);
            Assert.Equal(1, fault.Commits);
            Assert.Equal(1, fault.Faults);
            Assert.Equal(1, fault.Disposals);
            Assert.Equal(1, client.Bridge.ParSends);
            Assert.Equal(1, client.Bridge.TokenSends);
            Assert.Equal(before, await NativeAccountPersistence.CountAsync(issuer));
        }
        finally { fault.Armed = false; }
    }

    /// <summary>Completes a real account-authorized native exchange as an independent positive capability control.</summary>
    private static async Task ExchangeAsync(NativeClientHttp client, AdministratorProtocolScenario issuer)
    {
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(client);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        NativeClientInteroperabilityTests.RequireAccepted(await client.InvokeAsync(callback, challenge.Cookie));
    }

    /// <summary>Resolves and cryptographically validates the unpublished reference through native handlers, then independently checks its exact committed valid row.</summary>
    private static Task<bool> RequireRetainedAsync(NativeClientHttp client, NativeClientProfile profile, NativeClientCreationFault fault) =>
        new ProtocolStorageRequest(client.Services.GetRequiredService<TimeProvider>(), TestContext.Current.CancellationToken)
            .RunAsync(client.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                var transaction = await scope.Services.GetRequiredService<IOpenIddictClientFactory>().CreateTransactionAsync();
                transaction.BaseUri = profile.ClientUri;
                var context = new OpenIddictClientEvents.ValidateTokenContext(transaction) { Token = fault.State };
                context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken);
                await scope.Services.GetRequiredService<IOpenIddictClientDispatcher>().DispatchAsync(context);
                Assert.False(context.IsRejected);
                Assert.True(context.IsReferenceToken);
                Assert.True(context.TokenValidationResult?.IsValid == true);
                Assert.False(context.IsRequestHandled);
                Assert.False(context.IsRequestSkipped);
                var principal = Assert.IsType<ClaimsPrincipal>(context.Principal);
                Assert.True(principal.GetTokenId() == fault.Identifier);
                Assert.Equal(OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, principal.GetTokenType());
                Assert.Equal(AdministratorProtocolIdentity.ClientId, principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId));
                var status = await scope.Owner.RunAsync(current => new ValueTask<string?>(scope.Owner.Database
                    .Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Id == fault.Identifier)
                    .Select(row => row.Status).SingleAsync(current)), token);
                Assert.Equal(OpenIddictConstants.Statuses.Valid, status);
                return true;
            });
}
