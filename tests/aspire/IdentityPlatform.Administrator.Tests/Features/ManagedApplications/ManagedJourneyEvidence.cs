using System.Net;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Retains exact ordinary-rejection evidence without normalizing away native consumption, concurrency or replay effects.</summary>
internal static class ManagedJourneyEvidence
{
    private const string AccessToken = "access_token";
    /// <summary>Hashes all clients' complete native token/authorization descriptors, policy, human state, audit and administrator authority.</summary>
    internal static async Task<string> DigestAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return FactorBinding.Digest(new
        {
            Human = await ManagedHumanEvidence.DigestAsync(test),
            Authority = await ManagedHumanEvidence.AuthorityAsync(test),
            Policies = await database.Set<ApplicationPolicy>().AsNoTracking().OrderBy(row => row.ApplicationId).ToArrayAsync(TestContext.Current.CancellationToken),
            Native = await NativeAsync(database)
        });
    }

    /// <summary>Includes every native token/authorization scalar and actual foreign-key binding across both clients; no redemption field is omitted.</summary>
    internal static async Task<string> NativeAsync(PlatformDbContext database, string? applicationId = null)
    {
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => applicationId == null || row.Application!.Id == applicationId).OrderBy(row => row.Id).Select(row => new
            {
                row.Id,
                Application = row.Application == null ? null : row.Application.Id,
                Authorization = row.Authorization == null ? null : row.Authorization.Id,
                row.ConcurrencyToken,
                row.CreationDate,
                row.ExpirationDate,
                row.Payload,
                row.Properties,
                row.RedemptionDate,
                row.ReferenceId,
                row.Status,
                row.Subject,
                row.Type
            }).ToArrayAsync(TestContext.Current.CancellationToken);
        var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
            .Where(row => applicationId == null || row.Application!.Id == applicationId).OrderBy(row => row.Id).Select(row => new
            {
                row.Id,
                Application = row.Application == null ? null : row.Application.Id,
                row.ConcurrencyToken,
                row.CreationDate,
                row.Properties,
                row.Scopes,
                row.Status,
                row.Subject,
                row.Type
            }).ToArrayAsync(TestContext.Current.CancellationToken);
        return FactorBinding.Digest(new
        {
            Tokens = tokens,
            Authorizations = authorizations
        });
    }

    /// <summary>Fingerprints one independent registration's complete native configuration, policy and grants before another application's lifecycle write.</summary>
    internal static async Task<string> RegistrationAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var snapshot = await Applications.ApplicationUpdateSnapshot.ReadAsync(database, test.Id, TestContext.Current.CancellationToken);
        return FactorBinding.Digest(new
        {
            snapshot.NativeDigest,
            snapshot.Policy,
            Grants = await NativeAsync(database, test.Id)
        });
    }

    /// <summary>Proves the rightful client's actual Basic/S256 exchange, signatures, recipient, original nonce and native audit bindings.</summary>
    internal static async Task ExchangeAsync(ManagedCacheTestContext test, ManagedTokenFlow flow, string code, bool denyManagement = false)
    {
        using var response = await flow.ExchangeAsync(code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        ManagedHumanHttp.NoAuthorityCookie(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var row = Assert.Single((await test.SnapshotAsync(applicationOnly: true)).Interactions, value => value.RequestId == flow.RequestId);
        await ManagedTokenOracle.ValidateAsync(test, flow, body.RootElement, row.UserId!, applicationOnly: true, multipleFlows: denyManagement);
        await ManagedTokenEvidence.IssuanceAuditAsync(test, applicationOnly: true, requestId: denyManagement ? flow.RequestId : null);
        if (denyManagement)
        {
            using var allowed = await test.Http.DirectGetAsync(Applications.ApplicationHttpFixture.ApiCollection);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            using var denied = await test.Http.DirectGetAsync(Applications.ApplicationHttpFixture.ApiCollection,
                credential: body.RootElement.GetProperty(AccessToken).GetString()!);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }
}
