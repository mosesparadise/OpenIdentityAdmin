using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Administrator.Tests.Fixtures.Processes;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves a real second process rereads same-revision native metadata after an independently observed governance lock wait.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedScopeProcessTests(BootstrapFixture fixture)
{
    private const string GovernanceWait = "%AdministratorGovernance%";
    private const string EmailScope = "email";
    private const string ChangedDisplay = "Changed email disclosure";
    private const string RejectedField = "originalRejected";
    private const string ChangedField = "currentChanged";
    private const string RevisionField = "revisionUnchanged";
    private const string Operation = "ManagedPolicy";

    private static readonly byte[] Acknowledgement = "ack\n"u8.ToArray();
    private static readonly byte[] Newline = "\n"u8.ToArray();
    private const int InputLimit = 65_536;

    /// <summary>A decision captured in another OS process cannot survive changed native consent display metadata with unchanged product revisions.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SecondProcessCapturedPolicy_When_NativeMetadataChangesDuringObservedGovernanceWait_Then_CurrentReadRejectsOriginalDecision(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await ManagedScopeTest.SeedAsync(test);
        await using var child = new DriverProcess(GovernanceDriverControl.None, DriverExecutable.Governance);
        await SendAsync(child, test, provider);
        using var ready = await child.ReadFrameAsync(DriverProcess.Ready);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(test.Token);
        await new CeremonyLocks(database).AcquireAsync(new(true, false, [], []), test.Token);
        var blocker = await DatabaseWaiters.IdentifyAsync(database, test.Token);
        await child.Input.WriteAsync(Acknowledgement, child.Token);
        await child.Input.FlushAsync(child.Token);
        await DatabaseWaiters.ObserveOwnedAsync(test.Scenario.Account.Journey.Lease, blocker, GovernanceWait, 1, child.Token);
        var native = await database.Set<OpenIddictEntityFrameworkCoreScope>().SingleAsync(row => row.Name == EmailScope, test.Token);
        native.DisplayName = ChangedDisplay;
        await database.SaveChangesAsync(test.Token);
        await transaction.CommitAsync(test.Token);
        using var result = await child.ReadFrameAsync(DriverProcess.Result);
        Assert.True(result.RootElement.GetProperty(RejectedField).GetBoolean());
        Assert.True(result.RootElement.GetProperty(ChangedField).GetBoolean());
        Assert.True(result.RootElement.GetProperty(RevisionField).GetBoolean());
        await child.RequireExitAsync(0);
    }

    /// <summary>Transfers bounded synthetic connection/protection inputs through a private pipe and clears the temporary buffer.</summary>
    private static async Task SendAsync(DriverProcess child, ApplicationMutationFixture test, string provider)
    {
        var journey = test.Scenario.Account.Journey;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Operation = Operation,
            Provider = provider,
            journey.Lease.RuntimeConnection,
            journey.IssuerSettings,
            ClientId = ApplicationMutationFixture.Client,
            ScopeName = EmailScope,
            AwaitAcknowledgement = true
        });
        try
        {
            Assert.True(bytes.Length < InputLimit);
            await child.Input.WriteAsync(bytes, child.Token);
            await child.Input.WriteAsync(Newline, child.Token);
            await child.Input.FlushAsync(child.Token);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
