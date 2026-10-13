using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Uses native Identity hashing and real transaction reads to distinguish rehash invalidation from an unrelated denial.</summary>
internal static class ProofRehash
{
    private const int EarlierIterations = 1000;
    private const string ExpectedNativeChange = "Native rehash must change the password hash and security stamp before rollback.";
    private const string ExpectedOriginalBinding = "The original stored session must retain its original security binding.";

    /// <summary>Requires a real rehash inside the owning transaction and exact rollback of credential, authority and audit state.</summary>
    internal static async Task RequireAsync(GovernanceScenario scenario)
    {
        await PrepareAsync(scenario);
        var request = await GovernanceCommands.PrepareAsync(scenario, GovernanceCommands.RestoreGrant);
        var code = await scenario.UnusedCodeAsync();
        var before = await scenario.SnapshotAsync();
        await using (var operation = await GovernanceOperation.OpenAsync(scenario, request))
        {
            var actor = operation.State.Actor.User;
            var originalHash = actor.PasswordHash;
            var originalStamp = actor.SecurityStamp;
            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,
                operation.Get<IPasswordHasher<PlatformUser>>().VerifyHashedPassword(actor, originalHash!, scenario.Password));
            var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await Assert.ThrowsAsync<FactorUnavailableException>(() => operation.VerifyAsync(scenario, code));
            FactorOracle.RequireWindow(scenario.Journey.Seed, code, started, true);
            await RequireNativeWriteAsync(scenario, operation, originalHash, originalStamp);
        }
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Installs a real lower-iteration native hash as fixture state while preserving the original issued session binding.</summary>
    private static async Task PrepareAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var actor = await database.Users.SingleAsync(row => row.Id == scenario.ActorId, token);
        var hasher = new PasswordHasher<PlatformUser>(Options.Create(new PasswordHasherOptions { IterationCount = EarlierIterations }));
        actor.PasswordHash = hasher.HashPassword(actor, scenario.Password);
        await database.SaveChangesAsync(token);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<PlatformUser>>().VerifyHashedPassword(actor, actor.PasswordHash, scenario.Password));
    }

    /// <summary>Reads SQL through detached queries in the original transaction; tracked manager changes alone cannot satisfy this witness.</summary>
    private static async Task RequireNativeWriteAsync(GovernanceScenario scenario, GovernanceOperation operation,
        string? originalHash, string? originalStamp)
    {
        var database = operation.Get<PlatformDbContext>();
        var token = operation.Transaction.Token;
        var stored = await database.Users.AsNoTracking().SingleAsync(row => row.Id == scenario.ActorId, token);
        Assert.True(stored.PasswordHash != originalHash && stored.SecurityStamp != originalStamp, ExpectedNativeChange);
        Assert.Equal(PasswordVerificationResult.Success,
            operation.Get<IPasswordHasher<PlatformUser>>().VerifyHashedPassword(stored, stored.PasswordHash!, scenario.Password));
        var session = await database.Set<AdministratorSession>().AsNoTracking()
            .SingleAsync(row => row.PublicId == scenario.Grant.SessionId, token);
        Assert.True(session.SecurityStamp == originalStamp && session.SecurityStamp != stored.SecurityStamp, ExpectedOriginalBinding);
    }
}
