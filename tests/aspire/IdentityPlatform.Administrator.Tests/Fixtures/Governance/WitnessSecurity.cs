using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Prepares genuine native security events or explicitly labeled fixture corruption against one coherent witness.</summary>
internal sealed record WitnessSecurity(string Id, string Name, string Session)
{
    internal const string Lockout = "lockout";
    internal const string Recovery = "recovery";
    internal const string RestrictedRecovery = "restricted-recovery";
    internal const string Suspension = "suspension";
    internal const string Disable = "disable";
    internal const string FactorChange = "active-factor-change";
    private const string WitnessSuffix = "-witness";
    private const string WrongPassword = "synthetic-wrong-witness-password";
    private const string Login = "Login";
    private const string RecoveryPurpose = "Recovery";
    private const int EarlierFailures = 4;
    private const int RecoveryCount = 10;
    private const int FirstRecoveryGeneration = 1;
    private const int OperationSeconds = 5;

    /// <summary>Gives the genuine witness a stable later-sorting ID so one seeded governance-only lock omission is deterministic.</summary>
    internal static async Task<WitnessSecurity> CreateAsync(GovernanceScenario scenario)
    {
        var id = await GovernanceAccounts.AddAsync(scenario, userId: scenario.ActorId + WitnessSuffix);
        Assert.True(StringComparer.Ordinal.Compare(scenario.ActorId, id) < 0);
        await using var scope = scenario.CreateScope();
        var name = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.Where(row => row.Id == id)
            .Select(row => row.UserName).SingleAsync(TestContext.Current.CancellationToken);
        var session = await GovernanceAccounts.LoginAsync(scenario, id);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<JourneySessions>()
            .ValidateAsync(id, session.SessionId, TestContext.Current.CancellationToken));
        return new WitnessSecurity(id, Assert.IsType<string>(name), session.SessionId);
    }

    /// <summary>Completes only preparation before arming the race; the returned action owns the actual final security transaction.</summary>
    internal async Task<Func<CancellationToken, Task>> PrepareAsync(GovernanceScenario scenario, string variant)
    {
        if (variant == Lockout)
        {
            for (var index = 0; index < EarlierFailures; index++)
            {
                await FailPasswordAsync(scenario, TestContext.Current.CancellationToken);
            }
            return token => FailPasswordAsync(scenario, token);
        }
        if (variant is Recovery or RestrictedRecovery)
        {
            var code = await ProvisionRecoveryAsync(scenario);
            var correlation = CeremonySecrets.Create();
            await using var scope = scenario.CreateScope();
            var pending = await scope.ServiceProvider.GetRequiredService<JourneyPassword>().BeginAsync(Name, GovernanceAccounts.Password(scenario),
                RecoveryPurpose, CeremonySecrets.Create(), correlation, TestContext.Current.CancellationToken);
            Assert.NotNull(pending.Interaction);
            return token => RecoverAsync(scenario, pending.Interaction, correlation, code, token);
        }
        return token => CorruptAsync(scenario, variant, token);
    }

    /// <summary>Uses an actual wrong native password attempt; the fifth attempt must commit mandatory native/product lockout.</summary>
    private async Task FailPasswordAsync(GovernanceScenario scenario, CancellationToken token)
    {
        await using var scope = scenario.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<JourneyPassword>().BeginAsync(Name, WrongPassword, Login,
            CeremonySecrets.Create(), CeremonySecrets.Create(), token);
        Assert.Null(result.Interaction);
        Assert.False(result.NeedsDummyWork);
    }

    /// <summary>Provisions synthetic witness recovery material through real native generation, explicitly as fixture setup rather than shipped onboarding.</summary>
    private async Task<string> ProvisionRecoveryAsync(GovernanceScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        using var operation = services.GetRequiredService<NativeOperation>().Enter(token);
        var user = await database.Users.SingleAsync(row => row.Id == Id, token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == Id, token);
        Assert.Equal(0, enrollment.RecoveryGeneration);
        enrollment.RecoveryGeneration = FirstRecoveryGeneration;
        await database.SaveChangesAsync(token);
        using var selection = services.GetRequiredService<FactorAccess>().SelectRecovery(enrollment, token);
        var generated = await services.GetRequiredService<UserManager<PlatformUser>>().GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCount);
        Assert.NotNull(generated);
        var codes = generated.ToArray();
        Assert.Equal(RecoveryCount, codes.Distinct(StringComparer.Ordinal).Count());
        await transaction.CommitAsync(token);
        return codes[0];
    }

    /// <summary>Enters actual restricted recovery with the fresh password-bound interaction and generated native one-use code.</summary>
    private static async Task RecoverAsync(GovernanceScenario scenario, string interaction, string correlation, string code, CancellationToken token)
    {
        await using var scope = scenario.CreateScope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<JourneyTransition>().AuthorizeAsync(interaction, correlation,
            RecoveryPurpose, code, CeremonySecrets.Create(), token));
    }

    /// <summary>Commits scoped test corruption under the exact witness lock; this is not a product disable, suspension or factor-rotation path.</summary>
    private async Task CorruptAsync(GovernanceScenario scenario, string variant, CancellationToken caller)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(TimeSpan.FromSeconds(OperationSeconds));
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var connection = new CeremonyConnection(database);
        await connection.OpenAsync(deadline.Token);
        await using var transaction = await database.Database.BeginTransactionAsync(deadline.Token);
        await new CeremonyLocks(database).AcquireAsync(new(false, false, [], [Id]), deadline.Token);
        var count = variant switch
        {
            Suspension => await database.Set<AdministratorEnrollment>().Where(row => row.UserId == Id)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Suspended, true), deadline.Token),
            Disable => await database.Users.Where(row => row.Id == Id)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), deadline.Token),
            FactorChange => await database.Set<AdministratorFactor>().Where(row => row.UserId == Id).ExecuteDeleteAsync(deadline.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        Assert.Equal(1, count);
        await transaction.CommitAsync(deadline.Token);
    }
}
