using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Verifies atomic native registration creation, exact repeat and conflict rejection against the real provider transaction.</summary>
internal static class NativeRegistrationInitialization
{
    private const string Operator = "native-registration-test";
    private const string AuditEvent = "administrator-protocol-initialization";

    /// <summary>Requires rollback after native saves, denies a partial existing scope, then verifies creation and a write-free repeat.</summary>
    internal static async Task VerifyAsync(IServiceProvider root, CancellationToken token)
    {
        var policy = root.GetRequiredService<AdministratorRegistrationPolicy>();
        await VerifyAuditFailureAsync(root, policy, token);
        var expected = new ArithmeticException();
        var attempts = 0;
        var commit = root.GetRequiredService<NativeConsumptionCommit>();
        commit.BeforeCommit = () => { attempts++; throw expected; };
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => InitializeAsync(root, policy, token)));
        }
        finally { commit.BeforeCommit = null; }
        Assert.Equal(1, attempts);
        Assert.Null(await ReadAsync(root, token));
        await ChangePartialScopeAsync(root, true, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => InitializeAsync(root, policy, token));
        await ChangePartialScopeAsync(root, false, token);
        Assert.Null(await ReadAsync(root, token));
        Assert.True(await InitializeAsync(root, policy, token));
        var original = await ReadAsync(root, token);
        Assert.NotNull(original);
        Assert.False(await InitializeAsync(root, policy, token));
        Assert.Equal(original, await ReadAsync(root, token));
        await SetEnabledAsync(root, false, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => InitializeAsync(root, policy, token));
        Assert.False((await ReadAsync(root, token))!.Enabled);
        await SetEnabledAsync(root, true, token);
    }

    /// <summary>Fails the audit SaveChanges itself and independently requires rollback of earlier native saves and the staged profile.</summary>
    private static async Task VerifyAuditFailureAsync(IServiceProvider root, AdministratorRegistrationPolicy policy, CancellationToken token)
    {
        var fault = root.GetRequiredService<NativeRegistrationAuditFailure>();
        var expected = new ArithmeticException();
        var attempts = 0;
        fault.BeforeAuditSave = () => { attempts++; throw expected; };
        try
        {
            Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => InitializeAsync(root, policy, token)));
        }
        finally { fault.BeforeAuditSave = null; }
        Assert.Equal(1, attempts);
        Assert.Null(await ReadAsync(root, token));
    }

    /// <summary>Invokes the actual bounded Migrator operation with a new explicit correlation identifier.</summary>
    private static Task<bool> InitializeAsync(IServiceProvider root, AdministratorRegistrationPolicy policy, CancellationToken token) =>
        AdministratorRegistrationInitializer.RunAsync(root, policy, Operator, Guid.NewGuid(), token);

    /// <summary>Observes app/scope/manifest/audit atomically through an independent context, including exact no-op lock revisions.</summary>
    private static Task<Snapshot?> ReadAsync(IServiceProvider root, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync<Snapshot?>(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var profile = await scope.Owner.RunAsync(current => new ValueTask<AdministratorProtocolProfile?>(scope.Owner.Database.Set<AdministratorProtocolProfile>()
                .AsNoTracking().SingleOrDefaultAsync(current)), cancellation);
            var audit = await scope.Owner.RunAsync(current => new ValueTask<SecurityAuditIntent?>(scope.Owner.Database.Set<SecurityAuditIntent>()
                .AsNoTracking().SingleOrDefaultAsync(row => row.Event == AuditEvent, current)), cancellation);
            var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            var application = await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, cancellation);
            var resource = await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, cancellation);
            if (profile is null)
            {
                Assert.Null(audit);
                Assert.Null(application);
                Assert.Null(resource);
                return null;
            }
            Assert.NotNull(application);
            Assert.NotNull(resource);
            Assert.NotNull(audit);
            Assert.Equal(profile.ApplicationId, await applications.GetIdAsync(application, cancellation));
            Assert.Equal(profile.ScopeId, await scopes.GetIdAsync(resource, cancellation));
            return new(profile.ApplicationId, profile.ScopeId, profile.Revision, profile.LockRevision, profile.ObservedAt, profile.Enabled, audit.Id, audit.CorrelationId);
        });

    /// <summary>Creates and removes only a disposable native scope to prove that partial prior state is never adopted or repaired.</summary>
    private static Task<bool> ChangePartialScopeAsync(IServiceProvider root, bool create, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            if (create)
            {
                await manager.CreateAsync(AdministratorRegistrationPolicy.Scope(), cancellation);
            }
            else
            {
                await manager.DeleteAsync((await manager.FindByNameAsync(AdministratorProtocolIdentity.Scope, cancellation))!, cancellation);
            }
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>Changes only the fixture profile flag under the same owning profile lock used by runtime verification.</summary>
    private static Task<bool> SetEnabledAsync(IServiceProvider root, bool enabled, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            await scope.Owner.RunAsync(async current =>
            {
                var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current);
                profile.Enabled = enabled;
                await scope.Owner.Database.SaveChangesAsync(current);
            }, cancellation, write: true);
            await transaction.CommitAsync(cancellation);
            return true;
        });

    /// <summary>Retains only independent public identity, revision and audit observations for exact-repeat comparison.</summary>
    private sealed record Snapshot(string Application, string Scope, long Revision, long LockRevision, long Watermark, bool Enabled, Guid Audit, Guid Correlation);
}
