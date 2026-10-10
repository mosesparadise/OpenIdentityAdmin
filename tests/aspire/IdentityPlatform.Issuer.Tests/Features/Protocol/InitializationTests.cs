using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class InitializationTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly (string Key, string? Value)[] InvalidSettings =
    [
        ("Pilot:UserName", null), ("Pilot:UserName", "changed.user"), ("Pilot:Password", null),
        ("Pilot:Password", "incorrect-synthetic-password"), ("Pilot:ClientSecret", null),
        ("Pilot:ClientSecret", new string('x', 40)),
        ("Pilot:RedirectUri", "http://localhost/callback"), ("Pilot:RedirectUri", "https://localhost/changed"),
        ("Pilot:RedirectUri", "https://user:secret@localhost/callback"),
        ("Pilot:RedirectUri", "https://localhost/callback?extra=value"), ("Pilot:RedirectUri", "https://localhost/callback#fragment")
    ];
    public static IEnumerable<TheoryDataRow<string, string, string?>> InvalidCases =>
        from provider in Providers
        from value in InvalidSettings
        select new TheoryDataRow<string, string, string?>(provider, value.Key, value.Value).WithTrait("Provider", provider);

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Repeating_explicit_bootstrap_is_audited_without_changing_credentials_or_owned_ids(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var before = await SnapshotAsync(environment);
        await InitializeAsync(environment);
        await InitializeAsync(environment);
        var after = await SnapshotAsync(environment);
        Assert.True(before.PasswordHash == after.PasswordHash, "Bootstrap changed an existing password hash.");
        Assert.Equal(before.UserId, after.UserId);
        Assert.Equal(before.ApplicationId, after.ApplicationId);
        Assert.Equal(before.ScopeId, after.ScopeId);
        Assert.Equal(before.AuditCount + 2, after.AuditCount);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true)).Validated);
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task Invalid_or_drifted_bootstrap_inputs_do_not_repair_existing_state(string provider, string key, string? value)
    {
        var environment = await fixture.GetAsync(provider);
        var before = await SnapshotAsync(environment);
        var error = await Assert.ThrowsAsync<PilotConfigurationException>(() => InitializeAsync(environment, key, value));
        Assert.Equal(new PilotConfigurationException().Message, error.Message);
        var after = await SnapshotAsync(environment);
        Assert.True(before.PasswordHash == after.PasswordHash, "A rejected bootstrap changed the password hash.");
        Assert.Equal(before.UserId, after.UserId);
        Assert.Equal(before.ApplicationId, after.ApplicationId);
        Assert.Equal(before.ScopeId, after.ScopeId);
        Assert.Equal(before.AuditCount, after.AuditCount);
    }

    private static async Task InitializeAsync(IssuerEnvironment environment, string? key = null, string? value = null)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(environment.Services.GetRequiredService<IConfiguration>().AsEnumerable()).Build();
        if (key is not null)
        {
            configuration[key] = value;
        }
        var bootstrap = ActivatorUtilities.CreateInstance<PilotBootstrap>(scope.ServiceProvider, configuration);
        await bootstrap.InitializeAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Snapshot> SnapshotAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().FindByNameAsync(environment.Username))!;
        var manifest = await database.Set<PilotManifest>().SingleAsync(TestContext.Current.CancellationToken);
        var count = await database.Set<SecurityAuditIntent>().CountAsync(value => value.Event == "pilot-initialization", TestContext.Current.CancellationToken);
        return new Snapshot(user.Id, manifest.ApplicationId, manifest.ScopeId, user.PasswordHash!, count);
    }

    // No generated record ToString: this contains a credential verifier.
    private sealed class Snapshot(string userId, string applicationId, string scopeId, string passwordHash, int auditCount)
    {
        public string UserId { get; } = userId;
        public string ApplicationId { get; } = applicationId;
        public string ScopeId { get; } = scopeId;
        public string PasswordHash { get; } = passwordHash;
        public int AuditCount { get; } = auditCount;
    }
}
