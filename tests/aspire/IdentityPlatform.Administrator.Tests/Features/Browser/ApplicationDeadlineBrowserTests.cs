using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks real API database contention against the original deadline and rollback-only authority boundary.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationDeadlineBrowserTests(BootstrapFixture fixture)
{
    private const int Success = 200;
    private const int Unavailable = 503;
    private const int MaximumSeconds = 8;

    /// <summary>A native observed governance wait ends without releasing its blocker, commits no authority state and leaves the API reusable.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_NativeGovernanceLock_When_OriginalApiDeadlineEnds_Then_RequestDeniesAndAllAuthorityRollsBack(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
            await using var browser = await BrowserSession.CreateAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, await browser.Context.NewPageAsync());
            await ApplicationAccessFixture.RequireAsync(environment.Api!.Address, access.Value, Success);
            var before = await SnapshotAsync(environment);
            await RequireBlockedDeadlineAsync(environment, access.Value);
            await environment.Issuer.Account.Journey.Lease.AssertRuntimeDrainedAsync();
            Assert.Equal(before, await SnapshotAsync(environment));
            await ApplicationAccessFixture.RequireAsync(environment.Api.Address, access.Value, Success);
            Assert.Equal(before, await SnapshotAsync(environment));
        });

    /// <summary>Joins both the real request and native wait observer before releasing the independently owned database blocker.</summary>
    private static async Task RequireBlockedDeadlineAsync(BrowserHttpsFixture environment, string access)
    {
        var lease = environment.Issuer.Account.Journey.Lease;
        await using var blocker = await GovernanceLock.AcquireAsync(lease);
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var started = Stopwatch.GetTimestamp();
        var subject = ApplicationAccessFixture.RequireAsync(environment.Api!.Address, access, Unavailable);
        var observed = blocker.ObserveOwnedAsync(lease, 1, probe.Token);
        try
        {
            Assert.Same(observed, await Task.WhenAny(subject, observed));
            await observed;
            await subject.WaitAsync(TimeSpan.FromSeconds(MaximumSeconds), TestContext.Current.CancellationToken);
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(MaximumSeconds));
        }
        finally
        {
            await probe.CancelAsync();
            try
            {
                await observed;
            }
            catch (OperationCanceledException) when (probe.IsCancellationRequested) { }
            await blocker.DisposeAsync();
            await subject;
        }
    }

    /// <summary>Hashes current authority and audit rows in memory; failure diagnostics never contain token or account payloads.</summary>
    private static async Task<string> SnapshotAsync(BrowserHttpsFixture environment)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Governance = await database.Set<AdministratorGovernance>().AsNoTracking().ToArrayAsync(token),
            Profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().ToArrayAsync(token),
            Accounts = await database.Users.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token),
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Audit = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        })));
    }
}
