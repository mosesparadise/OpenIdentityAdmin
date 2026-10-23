using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises migrated provider uniqueness while preserving independent anonymous rows with null authority bindings.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationSchemaTests(BootstrapFixture fixture)
{
    private const string DuplicateValue = "synthetic-bound-reference";
    private const string PostgresUnique = "23505";
    private const int SqlUniqueIndex = 2601;
    private const int SqlUniqueConstraint = 2627;

    /// <summary>Both native migrations admit independent null references and reject a repeated populated binding without deleting either row.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_TwoAnonymousRows_When_ABindingIsDuplicated_Then_NativeUniqueIndexRejectsAndOriginalRowsRemain(string provider, UniqueBinding binding)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var first = IssuerContinuationProviderFixture.Create();
        var second = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, first));
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, second));
        await using (var scope = scenario.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await database.Set<IssuerContinuation>().SingleAsync(value => value.IntentDigest == first.Request.IntentDigest, TestContext.Current.CancellationToken);
            Set(row, binding);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        await using (var scope = scenario.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await database.Set<IssuerContinuation>().SingleAsync(value => value.IntentDigest == second.Request.IntentDigest, TestContext.Current.CancellationToken);
            Set(row, binding);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.True(error.InnerException is Npgsql.PostgresException { SqlState: PostgresUnique } ||
                error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlUniqueIndex or SqlUniqueConstraint });
        }
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Corrupts only disposable schema-test metadata; none of these rows is used to manufacture authorization.</summary>
    private static void Set(IssuerContinuation row, UniqueBinding binding)
    {
        switch (binding)
        {
            case UniqueBinding.Interaction:
                row.InteractionId = DuplicateValue;
                break;
            case UniqueBinding.Completion:
                row.CompletionDigest = IssuerContinuationProviderFixture.Digest(DigestSelector);
                break;
            case UniqueBinding.Request:
                row.RequestId = DuplicateValue;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(binding));
        }
    }

    private const string DigestSelector = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";

    /// <summary>Discovers each populated binding uniqueness constraint on each actual provider.</summary>
    public static IEnumerable<TheoryDataRow<string, UniqueBinding>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from binding in Enum.GetValues<UniqueBinding>()
        select new TheoryDataRow<string, UniqueBinding>(provider, binding).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);
}

/// <summary>Names the schema bindings that may be absent but cannot alias another continuation when populated.</summary>
public enum UniqueBinding
{
    Interaction, Completion, Request
}
