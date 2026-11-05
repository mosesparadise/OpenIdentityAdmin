using System.Globalization;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves independent-node aggregate races and actual native-save/audit rollback on both database providers, without claiming HTTP endpoint acceptance.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationUpdateTests(BootstrapFixture fixture)
{
    private const int FutureRevision = 2;
    private const int Updated = 200;
    private const int SqlDenied = 229;
    private const string PostgresDenied = "42501";
    private const int Contenders = 2;
    private const int ClockAdvance = 10;
    private const int ExpiryMinutes = 40;
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string FirstDisplay = "First edit";
    private const string SecondDisplay = "Second edit";
    private const string FirstCallback = "https://first.example.test/callback";
    private const string SecondCallback = "https://second.example.test/callback";

    /// <summary>Two independent service roots block on the actual provider singleton before distinct commands compete for one exact aggregate version.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentNodesAndOneVersion_When_DistinctUpdatesActuallyContend_Then_OnlyWinnerStateAndAuditCommit(string provider, bool firstReplacement, bool secondReplacement)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var peer = test.Peer(provider);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, created.Application!.Id);
        var warm = ApplicationMutationCommand.Edit(created.Application.Id, ApplicationVersion.Create(created.Application.Id, FutureRevision), new(FirstDisplay, [FirstCallback]))!;
        Assert.Equal(412, (await test.ExecuteOnAsync(peer, warm, Guid.NewGuid(), test.Token)).Status);
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, created.Application.Id)).Digest);
        test.Clock.Now = test.Clock.Now.AddSeconds(ClockAdvance);
        var commands = new[] { Command(test, created, firstReplacement, false), Command(test, created, secondReplacement, true) };
        var correlations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var lease = test.Scenario.Account.Journey.Lease;
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var first = test.ExecuteOnAsync(test.Api, commands[0], correlations[0], test.Token);
        var second = test.ExecuteOnAsync(peer, commands[1], correlations[1], test.Token);
        var outcomes = await ReleaseAndJoinAsync(held, () => held.ObserveWaitersAsync(lease, Contenders), first, second);
        Assert.Equal([200, 412], outcomes.Select(value => value.Status).Order().ToArray());
        var winner = Array.FindIndex(outcomes, value => value.Status == Updated);
        var loser = outcomes[1 - winner];
        Assert.Null(loser.Application);
        Assert.Null(loser.Version);
        Assert.Null(loser.Credential);
        await ApplicationUpdateOracle.RequireAsync(test, before, commands[winner], outcomes[winner], correlations[winner]);
        Assert.Equal(commands[winner].Kind == ApplicationMutationKind.Edit, await test.AcceptsAsync(created.Application.Id, created.Credential!.ClientSecret));
        var committed = await ApplicationUpdateSnapshot.ReadAsync(test, created.Application.Id);
        Assert.Equal(412, (await test.ExecuteAsync(commands[1 - winner])).Status);
        Assert.Equal(committed.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, created.Application.Id)).Digest);
    }

    /// <summary>A real native UPDATE precedes either an injected post-save failure or provider-denied audit INSERT; exact rollback leaves the original version usable.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedNativeUpdate_When_SaveOrAuditFails_Then_ExactAggregateRollsBackAndOriginalVersionStillWorks(string provider, bool replacement, bool denyAudit)
    {
        var fault = new ApplicationUpdateFault(!denyAudit);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        fault.ApplicationId = created.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, fault.ApplicationId);
        test.Clock.Now = test.Clock.Now.AddSeconds(ClockAdvance);
        var command = Command(test, created, replacement, false);
        fault.Armed = true;
        await FailAsync(test, command, denyAudit, fault);
        Assert.NotNull(fault.Persisted);
        Assert.NotEqual(before.NativeDigest, fault.Persisted.NativeDigest);
        ApplicationUpdateOracle.RequireState(test, before, fault.Persisted, command);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Audits), ApplicationUpdateSnapshot.Hash(fault.Persisted.Audits));
        Assert.Equal(before.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, fault.ApplicationId)).Digest);
        Assert.True(await test.AcceptsAsync(fault.ApplicationId, created.Credential!.ClientSecret));
        fault.Armed = false;
        var correlation = Guid.NewGuid();
        var recovery = await test.ExecuteOnAsync(test.Api, command, correlation, test.Token);
        await ApplicationUpdateOracle.RequireAsync(test, before, command, recovery, correlation);
        Assert.Equal(!replacement, await test.AcceptsAsync(fault.ApplicationId, created.Credential.ClientSecret));
    }

    /// <summary>Uses distinct display/callback or explicitly requested expiry values so partial or losing writes cannot hide behind identical input.</summary>
    private static ApplicationMutationCommand Command(ApplicationMutationFixture test, ApplicationMutationResult created, bool replacement, bool second) => replacement
        ? ApplicationMutationCommand.Replace(created.Application!.Id, created.Version!,
            new(true, test.Clock.Now.AddMinutes(ExpiryMinutes + (second ? 1 : 0)).ToString(Format, CultureInfo.InvariantCulture)), test.Clock.Now)!
        : ApplicationMutationCommand.Edit(created.Application!.Id, created.Version!, new(second ? SecondDisplay : FirstDisplay, [second ? SecondCallback : FirstCallback]))!;

    /// <summary>Requires the exact provider permission error and restores only the disposable lease's audit grant before recovery.</summary>
    internal static async Task FailAsync(ApplicationMutationFixture test, ApplicationMutationCommand command, bool denyAudit, ApplicationUpdateFault fault)
    {
        if (!denyAudit)
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => test.ExecuteAsync(command));
            Assert.Same(fault.Failure, failure);
            return;
        }
        var lease = test.Scenario.Account.Journey.Lease;
        await IssuerAuditPermission.RunAsync(lease, async () =>
        {
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => test.ExecuteAsync(command));
            if (failure.InnerException is PostgresException postgres)
            {
                Assert.Equal(PostgresDenied, postgres.SqlState);
            }
            else
            {
                Assert.Equal(SqlDenied, Assert.IsType<SqlException>(failure.InnerException).Number);
            }
        });
    }

    /// <summary>Always releases the blocker and drains both operations, preserving primary wait-graph failures instead of leaving background writers alive.</summary>
    internal static async Task<ApplicationMutationResult[]> ReleaseAndJoinAsync(GovernanceLock held, Func<Task> observe, params Task<ApplicationMutationResult>[] requests)
    {
        List<Exception> failures = [];
        try
        {
            await observe();
        }
        catch (Exception failure) { failures.Add(failure); }
        try
        {
            await held.DisposeAsync();
        }
        catch (Exception failure) { failures.Add(failure); }
        ApplicationMutationResult[] results = [];
        try
        {
            results = await Task.WhenAll(requests);
        }
        catch (Exception failure) { failures.Add(failure); }
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
        return results;
    }
}
