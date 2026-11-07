using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Admits real HTTPS revocation after issuer races and reconciles native eligibility independently of worker return values.</summary>
internal static class GrantIssuerRevocation
{
    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string GuidFormat = "D";
    private const string Valid = "valid";
    private const string Inactive = "inactive";
    private const string Revoked = "revoked";
    private const int BatchSize = 8;

    /// <summary>Submits the current disabled application through genuine BFF cookie and CSRF validation.</summary>
    internal static async Task<GrantJobView> AcceptAsync(ManagedCacheTestContext test)
    {
        var policy = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        Assert.False(policy.Policy.Enabled);
        using var response = await test.Http.GrantAsync(test.Id, Body, ApplicationVersion.Create(test.Id, policy.Policy.Revision), Guid.NewGuid().ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<GrantAcceptance>(TestContext.Current.CancellationToken);
        Assert.NotNull(accepted);
        return accepted.Job;
    }

    /// <summary>Runs an actual fresh owned worker batch against the genuine HTTPS fixture's shared provider state.</summary>
    internal static Task<bool> BatchAsync(ManagedCacheTestContext test, string job) => GrantBatchOperation.ExecuteAsync(
        test.Http.Environment.Api!.Services.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(job),
        test.Http.Environment.Issuer.Account.Journey.Clock, TestContext.Current.CancellationToken);

    /// <summary>Requires exact eligibility counts, preserved redeemed/invalid records and unchanged target configuration after bounded completion.</summary>
    internal static async Task ReconcileAsync(ManagedCacheTestContext test)
    {
        var before = await NativeAsync(test);
        var policy = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        var accepted = await AcceptAsync(test);
        var authorizations = before.Authorizations.Count(row => row.Status == Valid);
        var tokens = before.Tokens.Count(row => row.Status is Valid or Inactive);
        var batches = (Math.Max(authorizations, tokens) + BatchSize - 1) / BatchSize + 1;
        for (var index = 0; index < batches; index++)
        {
            Assert.True(await BatchAsync(test, accepted.Id));
        }
        Assert.False(await BatchAsync(test, accepted.Id));
        var after = await NativeAsync(test);
        Assert.Equal(before.Authorizations.Select(row => row with { Status = row.Status == Valid ? Revoked : row.Status }), after.Authorizations);
        Assert.Equal(before.Tokens.Select(row => row with { Status = row.Status is Valid or Inactive ? Revoked : row.Status }), after.Tokens);
        await using var scope = test.Http.Scope();
        var history = await GrantAuditWitness.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), Guid.Parse(accepted.Id), TestContext.Current.CancellationToken);
        history.Require();
        Assert.Equal(GrantJobState.Succeeded, history.Job.State);
        Assert.Equal(authorizations, history.Job.AuthorizationCount);
        Assert.Equal(tokens, history.Job.TokenCount);
        Assert.Single(history.Approvals);
        var changed = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        Assert.Equal(policy.NativeDigest, changed.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(policy.Policy), ApplicationUpdateSnapshot.Hash(changed.Policy));
    }

    /// <summary>Reads native identities and lifecycle statuses from an independent detached context, without payload or credential diagnostics.</summary>
    private static async Task<Native> NativeAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return new(await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Application!.Id == test.Id)
            .OrderBy(row => row.Id).Select(row => new Entry(row.Id!, row.Status)).ToArrayAsync(token),
            await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Application!.Id == test.Id)
            .OrderBy(row => row.Id).Select(row => new Entry(row.Id!, row.Status)).ToArrayAsync(token));
    }

    /// <summary>Retains only safe native identity and status for exact independent comparisons.</summary>
    private sealed record Entry(string Id, string? Status);
    /// <summary>Keeps independently eligible native families separate.</summary>
    private sealed record Native(Entry[] Authorizations, Entry[] Tokens);
}
