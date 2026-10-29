using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Runs finite projection, literal-prefix and lookahead queries against both actual EF relational providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationReaderTests(BootstrapFixture fixture)
{
    private const int PagedClients = 3;
    private static readonly string[] Clients = ["A-one", "a-one", "A-two", "A%literal", "A_literal", "A[one]", "A\\path", "Ängel", "😀one", "A space "];
    private static readonly string[] Prefixes = ["A-", "a-", "A%", "A_", "A[", "A\\", "Ä", "😀", "A space ", "missing"];
    private const string Search = "search";
    private const string Page = "page";
    private const string PageSize = "pageSize";
    private const string Second = "2";
    private const string First = "1";
    private const string Three = "3";
    private const string SecretCanary = "private-application-property-canary";
    private const int OverflowLength = 201;
    private const string OverflowBypass = "application-projection-overflow-bypass";
    private const string Overflow = "overflow";
    private static readonly DateTimeOffset Expiry = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private const int IdentifierMaximum = 400;
    private const int DisplayMaximum = 200;
    private const int SupplementaryPairs = 100;
    private const string Supplementary = "😀";
    private const string BoundaryClient = "bounded-native-client";
    private static readonly (int IdentifierLength, string Display, bool Allowed)[] Boundaries =
    [
        (IdentifierMaximum, new string('x', DisplayMaximum), true),
        (IdentifierMaximum + 1, new string('x', DisplayMaximum), false),
        (IdentifierMaximum, new string('x', DisplayMaximum) + " ", false),
        (IdentifierMaximum, string.Concat(Enumerable.Repeat(Supplementary, SupplementaryPairs)), true),
        (IdentifierMaximum, string.Concat(Enumerable.Repeat(Supplementary, SupplementaryPairs + 1)), false)
    ];

    /// <summary>Both native SQL projections count the final UTF-16 response bounds, including supplementary Unicode and trailing whitespace.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeMetadataBoundaries_When_Projected_Then_ExactMaximumsSucceedAndOverflowFails(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var boundary in Boundaries)
        {
            var row = new OpenIddictEntityFrameworkCoreApplication { Id = new string('i', boundary.IdentifierLength), ClientId = BoundaryClient, DisplayName = boundary.Display };
            database.Add(row);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            if (boundary.Allowed)
            {
                var result = await ApplicationListReader.ReadAsync(database, ApplicationListQuery.Parse([])!, Expiry, TestContext.Current.CancellationToken);
                var summary = Assert.Single(result.Items);
                Assert.Equal(row.Id, summary.Id);
                Assert.Equal(boundary.Display, summary.DisplayName);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => ApplicationListReader.ReadAsync(database, ApplicationListQuery.Parse([])!, Expiry, TestContext.Current.CancellationToken));
            }
            database.Remove(row);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Case, Unicode and wildcard-looking prefixes preserve literal matching and never leak excluded native properties.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeApplications_When_LiteralPrefixesAreRead_Then_OnlyMatchingSafeSummariesAreReturned(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var client in Clients)
        {
            database.Add(new OpenIddictEntityFrameworkCoreApplication
            {
                Id = Guid.NewGuid().ToString(),
                ClientId = client,
                DisplayName = client,
                ClientSecret = SecretCanary,
                Properties = JsonSerializer.Serialize(new { SecretCanary })
            });
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreach (var prefix in Prefixes)
        {
            var query = ApplicationListQuery.Parse([new(Search, [prefix])])!;
            var page = await ApplicationListReader.ReadAsync(database, query, Expiry, TestContext.Current.CancellationToken);
            Assert.Equal(Clients.Where(value => value.StartsWith(prefix, StringComparison.Ordinal)).Order().ToArray(), page.Items.Select(item => item.ClientId).Order().ToArray());
            Assert.DoesNotContain(SecretCanary, JsonSerializer.Serialize(page), StringComparison.Ordinal);
            Assert.False(page.HasNextPage);
            Assert.Equal(Expiry, page.ExpiresAt);
        }
    }

    /// <summary>Stable native ordering and one-record lookahead yield nonoverlapping pages without a separate mutable count.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_MoreRowsThanPage_When_PagesAreRead_Then_OrderingAndLookaheadAreDeterministic(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var client in Clients.Take(PagedClients).Reverse())
        {
            database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = client });
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var first = await ApplicationListReader.ReadAsync(database, ApplicationListQuery.Parse([new(PageSize, [Second])])!, Expiry, TestContext.Current.CancellationToken);
        var last = await ApplicationListReader.ReadAsync(database, ApplicationListQuery.Parse([new(Page, [Second]), new(PageSize, [Second])])!, Expiry, TestContext.Current.CancellationToken);
        Assert.True(first.HasNextPage);
        Assert.False(last.HasNextPage);
        Assert.Equal(Clients.Take(PagedClients).Order(StringComparer.Ordinal), first.Items.Concat(last.Items).Select(item => item.ClientId));
        Assert.Equal(PagedClients, first.Items.Concat(last.Items).Select(item => item.Id).Distinct().Count());
        var beyond = await ApplicationListReader.ReadAsync(database, ApplicationListQuery.Parse([new(Page, [Three]), new(PageSize, [Second])])!, Expiry, TestContext.Current.CancellationToken);
        Assert.Empty(beyond.Items);
        Assert.False(beyond.HasNextPage);
    }

    /// <summary>A long native display field in the lookahead invalidates the whole page instead of truncating identities or silently dropping records.</summary>
    [Trait("CriticalDecision", "application-projection-overflow")]
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OverlongLookaheadMetadata_When_PageRead_Then_ProjectionFailsClosed(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await using var scope = lease.Runtime.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = First });
        database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = Overflow, DisplayName = new string('x', OverflowLength) });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var denied = false;
        try
        {
            await ApplicationListReader.ReadAsync(database, ApplicationListQuery.Parse([new(PageSize, [First])])!, Expiry, TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException) { denied = true; }
        Assert.True(denied, OverflowBypass);
    }
}
