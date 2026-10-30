using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Reads independent durable evidence without exposing protected codes, identifiers or form material in assertion output.</summary>
internal static class ManagedHumanEvidence
{
    /// <summary>Hashes exact ordered managed proof, ceremony, replay, budget, session and audit rows from a fresh provider context.</summary>
    internal static async Task<string> DigestAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return FactorBinding.Digest(new
        {
            Browsers = await database.Set<AdministratorBrowserBudget>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Requests = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.Status, row.Type, row.ExpirationDate, row.RedemptionDate }).ToArrayAsync(token),
            Managed = await database.Set<ManagedLoginInteraction>().AsNoTracking().OrderBy(row => row.SelectorDigest).ToArrayAsync(token),
            Ceremonies = await database.Set<AdministratorInteraction>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Replay = await database.Set<AdministratorReplay>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Audit = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        });
    }

    /// <summary>Hashes unchanged administrator sessions, issuer completion grants and BFF tickets across their actual separate databases.</summary>
    internal static async Task<string> AuthorityAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        await using var bff = test.Http.Environment.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessions = bff.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var token = TestContext.Current.CancellationToken;
        return FactorBinding.Digest(new
        {
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Grants = await database.Set<IssuerContinuation>().AsNoTracking().OrderBy(row => row.IntentDigest).ToArrayAsync(token),
            Tickets = await sessions.Set<AdminSessionTicket>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(token)
        });
    }

    /// <summary>Reads current shared password/MFA failure count independently of the managed response.</summary>
    internal static async Task<int> FailuresAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        return (await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorAuthentication>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Failures;
    }
}
