using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Requires worker integration with actual native application permissions and retained revoked authorizations introduced after admission.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueScopeDeletionReferenceTests(BootstrapFixture fixture)
{
    private const int Contender = 1;
    private const int TargetSnapshotCount = 3;
    private const string Client = "retained-scope-reference";
    private const string Name = "email";

    /// <summary>A reference committed while the worker visibly waits must prevent native scope and policy deletion, even for a revoked retained grant.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_AdmittedScopeDeletion_When_NativeReferenceCommitsDuringObservedWait_Then_WorkerRetainsScopeAndPolicy(string provider, bool retained)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var intent = await CatalogueDeletionTest.CreateAsync(test, scope: true);
        var accepted = (await CatalogueDeletionTest.AdmitAsync(test, intent)).Job!;
        var lease = test.Scenario.Account.Journey.Lease;
        await using var peer = test.Peer(provider);
        await using var held = await GovernanceLock.AcquireAsync(lease);
        var processing = CatalogueDeletionTest.ProcessAsync(test, accepted.Id, peer);
        ExceptionDispatchInfo? failure = null;
        string[] before = [];
        try
        {
            await held.ObserveOwnedAsync(lease, Contender, test.Token);
            await ReferenceAsync(test, retained);
            before = await CatalogueMutationEvidence.SnapshotAsync(test);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { await held.DisposeAsync(); }
        Assert.True(await processing);
        failure?.Throw();
        Assert.Equal(before.Take(TargetSnapshotCount), (await CatalogueMutationEvidence.SnapshotAsync(test)).Take(TargetSnapshotCount));
        await CatalogueDeletionTest.RequireTerminalAsync(test, accepted.Id, CatalogueDeletionFailure.Referenced);
    }

    /// <summary>Uses managers to persist the new reference independently; no stub or boolean substitutes for the native reference query.</summary>
    private static async Task ReferenceAsync(ApplicationMutationFixture test, bool retained)
    {
        await using var scope = test.Scope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor { ClientId = Client, ClientType = OpenIddictConstants.ClientTypes.Public };
        if (!retained)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + Name);
        }
        var application = await applications.CreateAsync(descriptor, test.Token);
        if (retained)
        {
            var authorization = new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = await applications.GetIdAsync(application, test.Token),
                Status = OpenIddictConstants.Statuses.Revoked,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent
            };
            authorization.Scopes.Add(Name);
            await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>().CreateAsync(authorization, test.Token);
        }
    }
}
