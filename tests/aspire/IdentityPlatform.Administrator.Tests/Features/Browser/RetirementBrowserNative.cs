using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Separates genuine administrator sign-in additions from the immutable native retirement baseline.</summary>
internal static class RetirementBrowserNative
{
    private const int SignInRows = 5;
    private const string AdministratorClient = "identity-admin-bff";

    /// <summary>Requires every original native identity, application binding and status to survive a non-grant logout or sign-in phase.</summary>
    internal static void Preserved(ApplicationGrantBrowserEvidence.NativeRow[] before, ApplicationGrantBrowserEvidence.NativeRow[] after)
    {
        foreach (var row in before)
        {
            Assert.Equal(row, Assert.Single(after, current => current.Authorization == row.Authorization && current.Id == row.Id));
        }
    }

    /// <summary>Admits only the exact five new native rows of the real administrator sign-in, never additions to the retired target or another client.</summary>
    internal static async Task<ApplicationGrantBrowserEvidence.NativeRow[]> AddedAsync(BrowserHttpsFixture environment, string target,
        ApplicationGrantBrowserEvidence.NativeRow[] before)
    {
        var after = await ApplicationGrantBrowserEvidence.SnapshotAsync(environment);
        Preserved(before, after);
        var added = after.Where(row => !before.Any(original => original.Authorization == row.Authorization && original.Id == row.Id)).ToArray();
        Assert.Equal(before.Length + SignInRows, after.Length);
        Assert.Equal(SignInRows, added.Length);
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(token);
        var application = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking()
            .SingleAsync(row => row.ClientId == AdministratorClient, token);
        Assert.Equal(AdministratorClient, application.ClientId);
        Assert.Equal(profile.ApplicationId, application.Id);
        Assert.NotEqual(target, application.Id);
        Assert.All(added, row => Assert.Equal(application.Id, row.Application));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, Assert.Single(added, row => row.Authorization).Status);
        await RequireTokensAsync(database, added, token);
        return added;
    }

    /// <summary>Checks the exact issued access/identity and consumed request/code rows without exposing credential payloads.</summary>
    private static async Task RequireTokensAsync(PlatformDbContext database, ApplicationGrantBrowserEvidence.NativeRow[] added, CancellationToken token)
    {
        var ids = added.Where(row => !row.Authorization).Select(row => row.Id).ToArray();
        var rows = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => ids.Contains(row.Id!))
            .Select(row => new { row.Type, row.Status }).ToArrayAsync(token);
        var expected = new Dictionary<string, string>
        {
            [OpenIddictConstants.TokenTypeIdentifiers.AccessToken] = OpenIddictConstants.Statuses.Valid,
            [OpenIddictConstants.TokenTypeIdentifiers.IdentityToken] = OpenIddictConstants.Statuses.Valid,
            [OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode] = OpenIddictConstants.Statuses.Redeemed,
            [OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken] = OpenIddictConstants.Statuses.Redeemed
        };
        Assert.Equal(expected.Count, rows.Length);
        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, Assert.Single(rows, row => row.Type == pair.Key).Status);
        }
    }
}
