using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Separately verifies complete persisted native history, preserving code consumption while distinguishing rollback from committed output loss.</summary>
internal sealed class PilotDeadlineRows(PilotGenerationEvidence consumption, PilotDeadlineRows.Row code, PilotDeadlineRows.Row[] before, string authorizations)
{
    private const int OutputCount = 2;
    internal string CodeId => consumption.CodeId;

    /// <summary>Captures the genuine unconsumed code and unrelated native inventory before any timestamp injection is armed.</summary>
    internal static async Task<PilotDeadlineRows> BeforeAsync(BrowserHttpsFixture environment, string subject)
    {
        var consumption = await PilotGenerationEvidence.BeforeAsync(environment, subject, TestContext.Current.CancellationToken);
        await using var scope = PilotWaitState.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await ReadAsync(database);
        return new(consumption, Assert.Single(rows, row => row.Id == consumption.CodeId), rows, await AuthorizationsAsync(database));
    }

    /// <summary>Reads original redemption from an independent connection while generation is still active.</summary>
    internal Task RequireConsumedAsync(BrowserHttpsFixture environment, CancellationToken token) => consumption.RequireConsumedAsync(environment, token);

    /// <summary>Requires immutable original bindings, durable redemption, exact generated artifact types and unchanged unrelated/native authorization history.</summary>
    internal async Task RequireAsync(BrowserHttpsFixture environment, PilotDeadlineProbe probe, bool committed)
    {
        await RequireConsumedAsync(environment, TestContext.Current.CancellationToken);
        await using var scope = PilotWaitState.Observer(environment);
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var after = await ReadAsync(database);
        var original = Assert.Single(after, row => row.Id == code.Id);
        Assert.Equal(code.Immutable, original.Immutable);
        Assert.All(before.Where(row => row.Id != code.Id), row => Assert.Equal(row.Digest, Assert.Single(after, value => value.Id == row.Id).Digest));
        var added = after.Where(row => before.All(prior => prior.Id != row.Id)).ToArray();
        Assert.Equal(before.Length + (committed ? OutputCount : 0), after.Length);
        Assert.Equal(committed ? OutputCount : 0, added.Length);
        if (committed)
        {
            Assert.True(added.Select(row => row.Type).ToHashSet(StringComparer.Ordinal).SetEquals([
                OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken]));
            Assert.All(added, row => Assert.True(row.Authorization == code.Authorization && row.Application == code.Application &&
                row.Subject == code.Subject && row.Status == OpenIddictConstants.Statuses.Valid));
            Assert.Contains(true, added.Select(row => row.Id == probe.AccessId && row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken));
        }
        Assert.Equal(authorizations, await AuthorizationsAsync(database));
    }

    /// <summary>Requires actual native replay to revoke every retained affected row without creating another credential pair.</summary>
    internal Task RequireReplayedAsync(BrowserHttpsFixture environment, bool committed) =>
        consumption.RequireReplayedAsync(environment, committed, TestContext.Current.CancellationToken);

    /// <summary>Hashes private native payload/scalars in memory, exposing only digest comparisons to assertions.</summary>
    private static async Task<Row[]> ReadAsync(PlatformDbContext database)
    {
        var rows = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).Select(row => new
        {
            row.Id,
            row.Type,
            row.Status,
            row.Subject,
            row.Payload,
            row.Properties,
            row.ReferenceId,
            row.CreationDate,
            row.ExpirationDate,
            row.RedemptionDate,
            row.ConcurrencyToken,
            Application = row.Application == null ? null : row.Application.Id,
            Authorization = row.Authorization == null ? null : row.Authorization.Id
        }).ToArrayAsync(TestContext.Current.CancellationToken);
        return rows.Select(row => new Row(row.Id!, row.Type, row.Status, row.Subject, row.Application, row.Authorization,
            FactorBinding.Digest(row), FactorBinding.Digest(new
            {
                row.Id,
                row.Type,
                row.Subject,
                row.Payload,
                row.Properties,
                row.ReferenceId,
                row.CreationDate,
                row.ExpirationDate,
                row.Application,
                row.Authorization
            }))).ToArray();
    }

    /// <summary>Preserves all authorization scalars and native owner associations without printing protected identifiers.</summary>
    private static async Task<string> AuthorizationsAsync(PlatformDbContext database) => FactorBinding.Digest(
        await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(row => row.Id).Select(row => new
        {
            row.Id,
            row.ConcurrencyToken,
            row.CreationDate,
            row.Properties,
            row.Scopes,
            row.Status,
            row.Subject,
            row.Type,
            Application = row.Application == null ? null : row.Application.Id
        }).ToArrayAsync(TestContext.Current.CancellationToken));

    /// <summary>Retains only private native identities and hashes; it has no record-generated diagnostic formatter.</summary>
    internal sealed class Row(string id, string? type, string? status, string? subject, string? application, string? authorization, string digest, string immutable)
    {
        internal string Id => id;
        internal string? Type => type;
        internal string? Status => status;
        internal string? Subject => subject;
        internal string? Application => application;
        internal string? Authorization => authorization;
        internal string Digest => digest;
        internal string Immutable => immutable;
    }
}
