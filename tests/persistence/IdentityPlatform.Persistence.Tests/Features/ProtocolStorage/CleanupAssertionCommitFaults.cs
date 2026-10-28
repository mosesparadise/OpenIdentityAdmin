using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Requires exact reached commit/disposal faults and independent durable outcomes instead of accepting generic request failure.</summary>
internal static class CleanupAssertionCommitFaults
{
    private const int ProofLifetime = 60;
    private const string ChangedTarget = "changed-after-cleanup-proof-commit";

    /// <summary>Separates precommit rollback, uncertain completion, actual disposal, cancellation and downstream failure.</summary>
    internal static async Task VerifyAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        await CommitAsync(fixture, after: false, token);
        await CommitAsync(fixture, after: true, token);
        await DisposalAsync(fixture, token);
        await LaterRollbackAsync(fixture, token);
        await ExpiryAsync(fixture, token);
        await CancellationAsync(fixture, token);
        await MutationAsync(fixture, duringDisposal: false, token);
        await MutationAsync(fixture, duringDisposal: true, token);
        await CleanupAssertionBindings.VerifyAsync(fixture, token);
    }

    /// <summary>A reached precommit fault leaves no row; lost acknowledgement leaves one consumed row but no receipt publication.</summary>
    private static async Task CommitAsync(CleanupAssertionFixture fixture, bool after, CancellationToken token)
    {
        var proof = fixture.Proof();
        var expected = new ArithmeticException();
        var reached = 0;
        var commits = fixture.First.GetRequiredService<NativeConsumptionCommit>();
        Action fault = () => { reached++; throw expected; };
        if (after)
        {
            commits.AfterCommit = fault;
        }
        else
        {
            commits.BeforeCommit = fault;
        }
        var probe = new CleanupAssertionProbe();
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token));
            Assert.True(Contains(error, expected));
        }
        finally { commits.BeforeCommit = null; commits.AfterCommit = null; }
        Assert.Equal(1, reached);
        Assert.Equal(0, probe.Reached);
        Assert.Null(probe.Receipt);
        Assert.Equal(after, await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        if (after)
        {
            await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        }
        else
        {
            await CleanupAssertionEligibility.AcceptedAsync(fixture.Second, proof.Text, token);
        }
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>A one-shot failure during actual postcommit child disposal prevents receipt escape while retaining the committed digest.</summary>
    private static async Task DisposalAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var fault = fixture.First.GetRequiredService<CleanupAssertionDisposalFault>();
        var expected = new ArithmeticException();
        var before = fault.Reached;
        var proof = fixture.Proof();
        var commits = fixture.First.GetRequiredService<NativeConsumptionCommit>();
        commits.AfterCommit = () => fault.Next = expected;
        var probe = new CleanupAssertionProbe();
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token));
            Assert.True(Contains(error, expected));
        }
        finally { commits.AfterCommit = null; fault.Next = null; }
        Assert.Equal(before + 1, fault.Reached);
        Assert.Equal(0, probe.Reached);
        Assert.Null(probe.Receipt);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>A later fresh cleanup transaction can roll back its own changes but cannot undo the earlier proof consumption.</summary>
    private static async Task LaterRollbackAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var proof = fixture.Proof();
        var expected = new ArithmeticException();
        var reached = 0;
        var probe = new CleanupAssertionProbe
        {
            BeforeTarget = async (authentication, _) =>
        {
            await ProtocolStorageChild.RunAsync<bool>(fixture.First.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
                CleanupAssertionOwnership.Receipt(authentication).Claim(scope, new(authentication.Transaction));
                await scope.Owner.RunAsync(async current =>
                {
                    var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(current);
                    profile.Enabled = false;
                    await scope.Owner.Database.SaveChangesAsync(current);
                }, cancellation, write: true);
                reached++;
                throw expected;
            });
        }
        };
        var error = await Assert.ThrowsAnyAsync<Exception>(() => CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token));
        Assert.True(Contains(error, expected));
        Assert.Equal(1, reached);
        Assert.Equal(1, probe.Reached);
        Assert.True(await CleanupAssertionFixture.ReadAsync(fixture.Second, (database, current) => database.Set<AdministratorProtocolProfile>()
            .AsNoTracking().Select(row => row.Enabled).SingleAsync(current), token));
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>Expiry at the real commit boundary yields no receipt although the independent proof row is already durable.</summary>
    private static async Task ExpiryAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var proof = fixture.Proof();
        var commits = fixture.First.GetRequiredService<NativeConsumptionCommit>();
        var reached = 0;
        commits.AfterCommit = () => { reached++; fixture.First.GetRequiredService<NativeConsumptionClock>().Set(fixture.Now.AddSeconds(ProofLifetime)); };
        try
        {
            await CleanupAssertionEligibility.DeniedAsync(fixture.First, proof.Text, token);
        }
        finally { commits.AfterCommit = null; fixture.First.GetRequiredService<NativeConsumptionClock>().Set(fixture.Now); }
        Assert.Equal(1, reached);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>Cancellation after actual commit stops receipt publication and cannot rearm the spent proof on the other root.</summary>
    private static async Task CancellationAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var reached = 0;
        var commits = fixture.First.GetRequiredService<NativeConsumptionCommit>();
        commits.AfterCommit = () => { reached++; cancellation.Cancel(); };
        var proof = fixture.Proof();
        var probe = new CleanupAssertionProbe();
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, cancellation.Token));
            Assert.True(Cancelled(error));
        }
        finally { commits.AfterCommit = null; }
        Assert.Equal(1, reached);
        Assert.Equal(0, probe.Reached);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
    }

    /// <summary>In-place input changes during the consumption await or actual disposal are detected against the original frozen request.</summary>
    private static async Task MutationAsync(CleanupAssertionFixture fixture, bool duringDisposal, CancellationToken token)
    {
        var proof = fixture.Proof();
        var commits = fixture.First.GetRequiredService<NativeConsumptionCommit>();
        var fault = fixture.First.GetRequiredService<CleanupAssertionDisposalFault>();
        var reached = 0;
        var probe = new CleanupAssertionProbe
        {
            BeforeDispatch = context =>
        {
            Action mutate = () => { reached++; context.Request.Token = ChangedTarget; };
            commits.AfterCommit = duringDisposal ? () => fault.BeforeDispose = mutate : mutate;
        }
        };
        try
        {
            var result = await CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token);
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, result.Error);
            Assert.Null(result.Transaction.GetProperty<DurableCleanupClientAssertion>(typeof(DurableCleanupClientAssertion).FullName!));
        }
        finally { commits.AfterCommit = null; fault.BeforeDispose = null; }
        Assert.Equal(1, reached);
        Assert.Equal(0, probe.Reached);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        await CleanupAssertionEligibility.AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>Requires the exact injected fault in the preserved causal chain, including provider cleanup aggregation.</summary>
    private static bool Contains(Exception actual, Exception expected) => ReferenceEquals(actual, expected) ||
        (actual is AggregateException aggregate && aggregate.InnerExceptions.Any(error => Contains(error, expected))) ||
        (actual.InnerException is { } inner && Contains(inner, expected));

    /// <summary>Requires cancellation rather than accepting any arbitrary provider or disposal failure.</summary>
    private static bool Cancelled(Exception error) => error is OperationCanceledException ||
        (error is AggregateException aggregate && aggregate.InnerExceptions.Any(Cancelled)) ||
        (error.InnerException is { } inner && Cancelled(inner));
}
