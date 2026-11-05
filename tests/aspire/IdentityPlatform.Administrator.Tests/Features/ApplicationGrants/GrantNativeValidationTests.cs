using System.Security.Claims;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Proves causal native entry rejection independently of client disable, revision, audience or cryptographic validation; these are isolated handler tests.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantNativeValidationTests(BootstrapFixture fixture)
{
    private const string Peer = "native-validation-peer";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>The identical unexpired synthetic principal passes the real native handler before a committed production batch and fails afterward; a peer still passes.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ValidNativeEntry_When_ProductionGrantBatchCommits_Then_OnlyTargetEntryValidationRejects(string provider, bool authorization)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        var peer = await test.Fixture.ExecuteAsync(test.Fixture.Create(Peer));
        Assert.Equal(201, peer.Status);
        await test.SeedAsync(test.Id, 1, 1);
        await test.SeedAsync(peer.Application!.Id, 1, 1);
        var targetPrincipal = await PrincipalAsync(test, test.Id, authorization);
        var peerPrincipal = await PrincipalAsync(test, peer.Application.Id, authorization);
        Assert.True(await AllowsAsync(test, targetPrincipal, authorization));
        Assert.True(await AllowsAsync(test, peerPrincipal, authorization));
        var accepted = await test.AdmitAsync(test.Intent());
        Assert.True(await test.BatchAsync(accepted.Id));
        Assert.True(targetPrincipal.GetExpirationDate() > test.Fixture.Clock.Now);
        Assert.False(await AllowsAsync(test, targetPrincipal, authorization));
        Assert.True(await AllowsAsync(test, peerPrincipal, authorization));
        (await GrantAuditWitness.ReadAsync(test, accepted.Id)).Require();
    }

    /// <summary>Captures one real manager-created identity in a prevalidated test principal; no signed-token or issuer-interoperability claim is made.</summary>
    private static async Task<ClaimsPrincipal> PrincipalAsync(GrantTestContext test, string application, bool authorization)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken)
            .SetExpirationDate(test.Fixture.Clock.Now + Lifetime);
        if (authorization)
        {
            return principal.SetAuthorizationId(await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
                .Where(row => row.Application!.Id == application).Select(row => row.Id).SingleAsync(test.Token));
        }
        return principal.SetTokenId(await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Application!.Id == application).Select(row => row.Id).SingleAsync(test.Token));
    }

    /// <summary>Invokes exactly one installed library entry handler in a fresh ordinary test observation scope, avoiding masking by the other family or product guards.</summary>
    private static async Task<bool> AllowsAsync(GrantTestContext test, ClaimsPrincipal principal, bool authorization)
    {
        await using var scope = test.Fixture.Scope();
        var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction
        {
            Logger = NullLogger.Instance,
            Options = new OpenIddictServerOptions { TimeProvider = test.Fixture.Clock }
        })
        {
            Principal = principal.Clone(),
            TokenId = principal.GetTokenId(),
            AuthorizationId = principal.GetAuthorizationId()
        };
        if (authorization)
        {
            await new OpenIddictServerHandlers.Protection.ValidateAuthorizationEntry(scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>()).HandleAsync(context);
        }
        else
        {
            await new OpenIddictServerHandlers.Protection.ValidateTokenEntry(scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>()).HandleAsync(context);
        }
        if (context.IsRejected)
        {
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
        }
        return !context.IsRejected;
    }
}
