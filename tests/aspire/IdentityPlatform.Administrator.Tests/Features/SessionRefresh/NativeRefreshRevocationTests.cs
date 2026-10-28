using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Proves that ordinary native revocation and its response cannot substitute for authenticated family cleanup.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeRefreshRevocationTests(BootstrapFixture fixture)
{
    private const string UnrelatedClient = "unrelated-refresh-client";
    private const string SyntheticError = "Synthetic invalid token";
    private const string SyntheticErrorUri = "https://example.test/error";
    /// <summary>Revoking a predecessor leaves its successor and authorization valid in independently reloaded native state.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_TwoLinkedRefreshEntries_When_NativeRevocationCommits_Then_OnlyTheTargetIsRevoked(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.CommitAsync(async (scope, _) =>
        {
            var context = new OpenIddictServerEvents.HandleRevocationRequestContext(await NativeRefreshStages.TransactionAsync(scope))
            {
                GenericTokenPrincipal = family.Predecessor
            };
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRejected);
            return true;
        });
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Revoked, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
        Assert.False(await scenario.RunAsync(async (scope, _) => (await NativeRefreshStages.ValidateAsync(scope, family.Successor)).IsRejected));
    }

    /// <summary>Even a synthetic prevalidated code principal is not a native revocation target; refresh is the positive control.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_PrevalidatedTokenTypes_When_NativeRevocationTypeRuns_Then_CodeCannotBeUsedAsCleanupTarget(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.RunAsync(async (scope, _) =>
        {
            foreach (var type in new[] { OpenIddictConstants.TokenTypeIdentifiers.RefreshToken, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode })
            {
                var context = new OpenIddictServerEvents.ValidateRevocationRequestContext(await NativeRefreshStages.TransactionAsync(scope))
                {
                    GenericTokenPrincipal = family.Predecessor.Clone(claim => true).SetTokenType(type)
                };
                await new OpenIddictServerHandlers.Revocation.ValidateTokenType().HandleAsync(context);
                Assert.Equal(type == OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, context.IsRejected);
                Assert.Equal(context.IsRejected ? OpenIddictConstants.Errors.UnsupportedTokenType : null, context.Error);
            }
            return true;
        });
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
    }

    /// <summary>Presenter validation rejects another client without mutating any native state.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_BoundRefreshPrincipal_When_RevokingClientChanges_Then_NativePartyValidationDeniesTheForeignClient(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.RunAsync(async (scope, _) =>
        {
            foreach (var client in new[] { NativeRefreshStages.Client, UnrelatedClient })
            {
                var transaction = await NativeRefreshStages.TransactionAsync(scope);
                Assert.NotNull(transaction.Request);
                transaction.Request.ClientId = client;
                var context = new OpenIddictServerEvents.ValidateRevocationRequestContext(transaction) { GenericTokenPrincipal = family.Predecessor };
                await new OpenIddictServerHandlers.Revocation.ValidateAuthorizedParty().HandleAsync(context);
                Assert.Equal(client != NativeRefreshStages.Client, context.IsRejected);
            }
            return true;
        });
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
    }

    /// <summary>Native invalid-token normalization removes error fields without changing valid family state; it is not revocation confirmation.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_InvalidTokenError_When_NativeResponseIsNormalized_Then_EmptyResponseProvesNoFamilyDenial(string provider)
    {
        await using var scenario = await NativeRefreshFixture.CreateAsync(fixture, provider);
        var family = await NativeRefreshRows.CreateAsync(scenario);
        await scenario.RunAsync(async (scope, _) =>
        {
            var transaction = await NativeRefreshStages.TransactionAsync(scope);
            transaction.Response = new OpenIddictResponse
            {
                Error = OpenIddictConstants.Errors.InvalidToken,
                ErrorDescription = SyntheticError,
                ErrorUri = SyntheticErrorUri
            };
            var context = new OpenIddictServerEvents.ApplyRevocationResponseContext(transaction);
            await new OpenIddictServerHandlers.Revocation.NormalizeErrorResponse().HandleAsync(context);
            Assert.Empty(context.Response.GetParameters());
            return true;
        });
        Assert.Equal((OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.Statuses.Valid),
            await NativeRefreshRows.StatusAsync(scenario, family));
    }

    /// <summary>Retains native discovery parity across both real database providers.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;
}
