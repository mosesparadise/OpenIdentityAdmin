using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Observes managed target and control rows independently of administrator authority records.</summary>
internal sealed record GrantAuthorityEvidence(ApplicationUpdateSnapshot Configuration, GrantAuthorityEvidence.Row[] Authorizations, GrantAuthorityEvidence.Row[] Tokens)
{
    private const string Valid = "valid";
    private const string Revoked = "revoked";
    internal const int Batch = 8;
    internal const int Population = Batch + 1;
    private const string Control = "authority-control";
    private const int Created = 201;

    /// <summary>Creates a separate managed application whose native authorization and token must remain untouched.</summary>
    internal static async Task<string> ControlAsync(GrantTestContext test)
    {
        var result = await test.Fixture.ExecuteAsync(test.Fixture.Create(Control));
        Assert.Equal(Created, result.Status);
        var id = result.Application!.Id;
        await test.SeedAsync(id, 1, 1);
        return id;
    }

    /// <summary>Captures persisted row identities, associations, types and statuses without credential or token payloads.</summary>
    internal static async Task<GrantAuthorityEvidence> ReadAsync(GrantTestContext test, string id)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return new(await ApplicationUpdateSnapshot.ReadAsync(database, id, test.Token),
            await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Application!.Id == id)
                .OrderBy(row => row.Id).Select(row => new Row(row.Id!, row.Application!.Id!, null, row.Type, row.Status)).ToArrayAsync(test.Token),
            await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Application!.Id == id)
                .OrderBy(row => row.Id).Select(row => new Row(row.Id!, row.Application!.Id!, row.Authorization!.Id, row.Type, row.Status)).ToArrayAsync(test.Token));
    }

    /// <summary>Requires unchanged target configuration and exact native membership, permitting only the expected number of valid-to-revoked changes.</summary>
    internal void Require(GrantAuthorityEvidence before, int revoked)
    {
        Assert.Equal(before.Configuration.NativeDigest, Configuration.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Configuration.Policy), ApplicationUpdateSnapshot.Hash(Configuration.Policy));
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Configuration.LifecycleAudits), ApplicationUpdateSnapshot.Hash(Configuration.LifecycleAudits));
        RequireRows(before.Authorizations, Authorizations, revoked);
        RequireRows(before.Tokens, Tokens, revoked);
    }

    /// <summary>Checks every independent row rather than inferring native effects from progress counters or array lengths.</summary>
    private static void RequireRows(Row[] before, Row[] after, int revoked)
    {
        Assert.Equal(before.Length, after.Length);
        Assert.Equal(revoked, after.Count(row => row.Status == Revoked) - before.Count(row => row.Status == Revoked));
        foreach (var original in before)
        {
            var current = Assert.Single(after, row => row.Id == original.Id);
            Assert.Equal(original, current with
            {
                Status = original.Status
            });
            Assert.True(current.Status == original.Status || original.Status == Valid && current.Status == Revoked);
        }
    }

    /// <summary>Requires exactly one durable zero-delta authority stop and proves a subsequent poll cannot silently restart the job.</summary>
    internal static async Task RequireStopAsync(GrantTestContext test, string id, GrantAuditWitness before)
    {
        var after = await GrantAuditWitness.ReadAsync(test, id);
        after.Require();
        after.Preserves(before);
        Assert.Equal(before.Details.Length + 1, after.Details.Length);
        Assert.Equal(before.Approvals.Length, after.Approvals.Length);
        Assert.Equal(GrantJobState.Stopped, after.Job.State);
        Assert.Equal(GrantJobFailure.Authority, after.Job.Failure);
        Assert.Equal(before.Job.AuthorizationCount, after.Job.AuthorizationCount);
        Assert.Equal(before.Job.TokenCount, after.Job.TokenCount);
        Assert.False(await test.BatchAsync(id));
        var unchanged = await GrantAuditWitness.ReadAsync(test, id);
        unchanged.Preserves(after);
        Assert.Equal(after.Details.Length, unchanged.Details.Length);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(after.Job), ApplicationUpdateSnapshot.Hash(unchanged.Job));
    }

    /// <summary>Contains only nonsecret native identity and state needed for independent effect verification.</summary>
    internal sealed record Row(string Id, string Application, string? Authorization, string? Type, string? Status);
}
