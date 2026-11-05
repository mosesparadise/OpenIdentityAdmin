using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Requires current persisted effective-account eligibility independently of the accepted job's retained approval.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantActorTests(BootstrapFixture fixture)
{
    private const string Disabled = "disabled";
    private const string Suspended = "suspended";
    private const string FactorDisabled = "factor-disabled";
    private const string FactorMissing = "factor-missing";
    private const string Locked = "locked";
    private const string PasswordMissing = "password-missing";
    private const string StampMissing = "stamp-missing";
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(5);

    /// <summary>One real account/factor change stops the next batch before native mutation and produces exactly one zero-delta stopped audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Disabled, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Suspended, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", FactorDisabled, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", FactorMissing, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Locked, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", PasswordMissing, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", StampMissing, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Disabled, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Suspended, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", FactorDisabled, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", FactorMissing, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Locked, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", PasswordMissing, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", StampMissing, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AcceptedJob_When_EffectiveAccountBecomesIneligible_Then_NativeEntriesStayValidAndWorkStops(string provider, string change)
    {
        await using var test = await GrantTestContext.CreateAsync(fixture, provider);
        await test.SeedAsync(test.Id, 1, 1);
        var accepted = await test.AdmitAsync(test.Intent());
        await ChangeAsync(test, change);
        Assert.True(await test.BatchAsync(accepted.Id));
        var history = await GrantAuditWitness.ReadAsync(test, accepted.Id);
        history.Require();
        Assert.Equal(GrantJobState.Stopped, history.Job.State);
        Assert.Equal(GrantJobFailure.Authority, history.Job.Failure);
        Assert.Equal(0, history.Job.TokenCount);
        Assert.Equal(2, history.Details.Length);
        Assert.Equal("valid", Assert.Single((await test.StatusesAsync(test.Id)).Tokens));
        Assert.False(await test.BatchAsync(accepted.Id));
    }

    /// <summary>Changes actual persisted eligibility through an independent fixture context without replacing the worker's authorization callback.</summary>
    private static async Task ChangeAsync(GrantTestContext test, string change)
    {
        await using var scope = test.Fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actor = test.Fixture.Identity.Subject;
        var user = await database.Users.SingleAsync(row => row.Id == actor, test.Token);
        switch (change)
        {
            case Disabled:
                user.IsEnabled = false;
                break;
            case FactorDisabled:
                user.TwoFactorEnabled = false;
                break;
            case PasswordMissing:
                user.PasswordHash = null;
                break;
            case StampMissing:
                user.SecurityStamp = null;
                break;
            case Locked:
                user.LockoutEnd = test.Fixture.Clock.Now + Lockout;
                break;
            case Suspended:
                (await database.Set<AdministratorEnrollment>().SingleAsync(row => row.UserId == actor, test.Token)).Suspended = true;
                break;
            case FactorMissing:
                Assert.Equal(1, await database.Set<AdministratorFactor>().Where(row => row.UserId == actor).ExecuteDeleteAsync(test.Token));
                break;
            default:
                throw new InvalidOperationException();
        }
        await database.SaveChangesAsync(test.Token);
    }
}
