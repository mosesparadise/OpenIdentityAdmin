using System.Diagnostics.CodeAnalysis;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol.Upgrade;

/// <summary>Owns the explicit atomic B2-to-B3 registration transition and same-operation reconciliation.</summary>
internal static class AdministratorRegistrationUpgrade
{
    private const int OperatorLength = 64;
    private const string Failure = "Administrator browser registration upgrade was rejected.";

    /// <summary>Preserves the original request and write budgets, waiting for real cleanup before reporting any result.</summary>
    internal static async Task<bool> RunAsync(IServiceProvider services, AdministratorRegistrationInput input, CancellationToken token)
    {
        Require(input.OperationId != Guid.Empty && input.ExpectedRevision is > 0 and < long.MaxValue &&
            input.OperatorReference is { Length: > 0 and <= OperatorLength } &&
            input.OperatorReference.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'));
        var browser = input.Policy.ForBrowserSessions();
        Require(browser is not null);
        var request = new ProtocolStorageRequest(services.GetRequiredService<TimeProvider>(), token);
        try
        {
            return await request.RunAsync(services.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                scope.Services.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await LockAsync(scope.Owner, cancellation);
                var snapshot = await AdministratorUpgradeSnapshot.ReadAsync(scope, input.Policy, cancellation);
                var receipt = await AdministratorUpgradeReceipt.ReadAsync(scope.Owner, input.OperationId, cancellation);
                if (browser.Matches(snapshot.Descriptor, snapshot.Scope))
                {
                    Require(snapshot.Profile.Revision == checked(input.ExpectedRevision.Value + 1) && AdministratorUpgradeReceipt.Matches(receipt, input));
                    return false;
                }
                Require(receipt.Length == 0 && snapshot.Profile.Revision == input.ExpectedRevision && input.Policy.Matches(snapshot.Descriptor, snapshot.Scope));
                await ApplyAsync(scope, snapshot, input, browser, cancellation);
                await transaction.CommitAsync(cancellation);
                return true;
            });
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Acquires governance before profile ownership with no tracked authority loaded before either boundary.</summary>
    private static async ValueTask LockAsync(ProtocolStorageOwner owner, CancellationToken token)
    {
        await owner.RunAsync(current => new ValueTask(new CeremonyLocks((PlatformDbContext)owner.Database)
            .AcquireAsync(new CeremonyLockPlan(true, false, [], []), current)), token, write: true);
        await AdministratorProtocolLocks.AcquireProfileAsync(owner, token);
    }

    /// <summary>Stages native metadata, pending-request invalidation, revision and audit in one transaction without changing enabled state.</summary>
    private static async ValueTask ApplyAsync(ProtocolStorageScope scope, AdministratorUpgradeSnapshot snapshot, AdministratorRegistrationInput input,
        AdministratorRegistrationPolicy browser, CancellationToken token)
    {
        await snapshot.ApplyAsync(scope, browser, token);
        await AdministratorPendingRequests.RevokeAsync(scope, snapshot.Profile.ApplicationId, token);
        await scope.Owner.RunAsync(async current =>
        {
            var sample = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
            Require(sample >= 0 && snapshot.Profile.ObservedAt - sample <= AdministratorAssertion.ClockSkewSeconds);
            scope.Owner.Database.Attach(snapshot.Profile);
            snapshot.Profile.ObservedAt = Math.Max(sample, snapshot.Profile.ObservedAt);
            snapshot.Profile.Revision = checked(input.ExpectedRevision!.Value + 1);
            scope.Owner.Database.Add(AdministratorUpgradeReceipt.Create(input, snapshot.Profile.ObservedAt));
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        var persisted = await AdministratorUpgradeSnapshot.ReadAsync(scope, input.Policy, token);
        Require(browser.Matches(persisted.Descriptor, persisted.Scope) && persisted.Profile.Revision == input.ExpectedRevision + 1 &&
            persisted.Profile.Enabled == snapshot.Profile.Enabled && persisted.Profile.ObservedAt == snapshot.Profile.ObservedAt);
        var finalSample = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        Require(finalSample >= 0 && persisted.Profile.ObservedAt - finalSample <= AdministratorAssertion.ClockSkewSeconds);
    }

    /// <summary>Rejects unsafe preconditions with one finite diagnostic and no submitted values.</summary>
    internal static void Require([DoesNotReturnIf(false)] bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException(Failure);
        }
    }
}
