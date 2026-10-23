using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Tests the real anonymous transaction mode cannot persist caller-supplied authority or arbitrary audits.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationBoundaryTests(BootstrapFixture fixture)
{
    private const string InvalidDigest = "invalid-digest";
    private const string Client = "identity-admin-bff";
    private const string ForgedAudit = "synthetic-forged-audit";
    private const string Account = "synthetic-account";
    private const int Lifetime = 300;
    private const int Retention = 330;

    /// <summary>Direct misuse of the bounded commit rejects before persistence, and the same genuine entry remains admissible afterward.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_AnonymousTransaction_When_CallerStagesAuthorityOrExtraAudit_Then_NothingCommits(string provider, AnonymousFault fault)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var attempt = IssuerContinuationProviderFixture.Create();
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        await using (var scope = scenario.CreateScope())
        {
            var services = scope.ServiceProvider;
            var database = services.GetRequiredService<PlatformDbContext>();
            await using var transaction = await CeremonyTransaction.OpenContinuationAsync(database,
                services.GetRequiredService<NativeOperation>(), TestContext.Current.CancellationToken);
            var row = Row(attempt, scenario.Clock.Now.ToUnixTimeSeconds());
            if (fault == AnonymousFault.Account)
            {
                row.UserId = Account;
            }
            if (fault == AnonymousFault.Digest)
            {
                row.IntentDigest = InvalidDigest;
            }
            if (fault == AnonymousFault.ExtraAudit)
            {
                database.Add(new SecurityAuditIntent { Event = ForgedAudit, ActorType = ForgedAudit, TargetType = ForgedAudit, Outcome = ForgedAudit });
            }
            database.Add(row);
            await Assert.ThrowsAsync<FactorUnavailableException>(() => transaction.CommitContinuationAsync(row, services.GetRequiredService<CeremonyClock>()));
        }
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
    }

    /// <summary>Supplies independent start-contract values only; no account or session is fabricated for an authentication operation.</summary>
    private static IssuerContinuation Row(IssuerContinuationProviderFixture.Attempt attempt, long now) => new()
    {
        IntentDigest = attempt.Request.IntentDigest,
        BrowserDigest = attempt.Request.BrowserDigest,
        ClientId = Client,
        CreatedAt = now,
        ExpiresAt = now + Lifetime,
        RetainUntil = now + Retention,
        ObservedAt = now,
        Revision = 1,
        CorrelationId = Guid.NewGuid()
    };

    /// <summary>Discovers each anonymous commit misuse against both migrated relational providers.</summary>
    public static IEnumerable<TheoryDataRow<string, AnonymousFault>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Enum.GetValues<AnonymousFault>()
        select new TheoryDataRow<string, AnonymousFault>(provider, fault).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);
}

/// <summary>Names distinct anonymous transaction authority and audit misuse controls.</summary>
public enum AnonymousFault
{
    Account, Digest, ExtraAudit
}
