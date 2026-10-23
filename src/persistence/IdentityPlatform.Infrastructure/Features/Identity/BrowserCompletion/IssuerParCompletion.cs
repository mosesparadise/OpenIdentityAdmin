using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Retains one completed ceremony under profile then admission ownership and stages attachment only in successful native PAR issuance.</summary>
internal sealed class IssuerParCompletion
{
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStoragePhase phase;
    private readonly IssuerContinuation row;
    private readonly BrowserLoginBinding binding;
    private readonly string completionDigest;
    private readonly string provenance;
    private readonly long revision;
    private bool attached;

    /// <summary>Captures the original transaction epoch and untracked row; capacity cleanup cannot accidentally save its clock observation.</summary>
    private IssuerParCompletion(ProtocolStorageScope scope, IssuerContinuation row, BrowserLoginBinding binding, string digest)
    {
        this.scope = scope;
        this.row = row;
        this.binding = binding;
        completionDigest = digest;
        provenance = Provenance(row);
        revision = row.Revision;
        phase = scope.Owner.Capture(write: true);
    }

    /// <summary>Acquires admission only after the caller's existing profile lock; no nested transaction or account authority is created.</summary>
    internal static async ValueTask<IssuerParCompletion> OpenAsync(ProtocolStorageScope scope, BrowserLoginBinding binding, CancellationToken token)
    {
        var owner = scope.Owner;
        if (owner.Database is not PlatformDbContext database || owner.Transaction is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await owner.RunAsync(cancellation => new ValueTask(new CeremonyLocks(database)
            .AcquireAsync(new CeremonyLockPlan(false, true, [], []), cancellation)), token, write: true);
        var row = await owner.RunAsync(cancellation => new ValueTask<IssuerContinuation?>(database.Set<IssuerContinuation>()
            .AsNoTracking().SingleOrDefaultAsync(row => row.IntentDigest == binding.IntentDigest, cancellation)), token);
        var digest = IssuerContinuationPolicy.Digest(binding.Completion)!;
        if (row is null || row.State != IssuerContinuationPolicy.Completed || row.CompletionDigest != digest ||
            row.RequestId is not null || row.ClientId != AdministratorProtocolIdentity.ClientId ||
            string.IsNullOrEmpty(row.UserId) || string.IsNullOrEmpty(row.SessionId) || row.SessionExpiresAt is null ||
            row.SessionExpiresAt <= owner.Clock.GetUtcNow().ToUnixTimeSeconds() ||
            !IssuerContinuationPolicy.Observe(row, owner.Clock.GetUtcNow().ToUnixTimeSeconds()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return new(scope, row, binding, digest);
    }

    /// <summary>Adds only private representation to the actual prepared native request and preserves its shorter original deadline.</summary>
    internal void BindPrincipal(ClaimsPrincipal principal, CancellationToken token)
    {
        Require(token);
        if (attached || principal.GetExpirationDate() is not { } expiry)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        binding.CopyTo(principal);
        principal.SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(Math.Min(expiry.ToUnixTimeSeconds(),
            Math.Min(row.ExpiresAt, row.SessionExpiresAt!.Value))));
    }

    /// <summary>Stages the actual native ID, immutable original binding and audit after verified token creation; the caller alone owns commit.</summary>
    internal async ValueTask AttachAsync(ClaimsPrincipal principal, OpenIddictRequest request, CancellationToken token)
    {
        Require(token);
        if (attached || !Current(principal, request, token) || principal.GetTokenId() is not { Length: > 0 } id ||
            id.Length > IssuerContinuationPolicy.ReferenceLength)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (!IssuerContinuationPolicy.Observe(row, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await scope.Owner.RunAsync(async cancellation =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            database.Attach(row);
            database.Entry(row).Property(value => value.ObservedAt).IsModified = true;
            row.State = IssuerContinuationPolicy.Attached;
            row.RequestId = id;
            row.Revision = checked(row.Revision + 1);
            IssuerContinuationAudit.Add(database, row, attached: true);
            await database.SaveChangesAsync(cancellation);
        }, token, write: true);
        attached = true;
        if (!Current(principal, request, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Rechecks exact native representation and original deadlines immediately before issuance commit.</summary>
    internal bool Current(ClaimsPrincipal principal, OpenIddictRequest request, CancellationToken token)
    {
        Require(token);
        return Provenance(row) == provenance &&
            row.State == (attached ? IssuerContinuationPolicy.Attached : IssuerContinuationPolicy.Completed) &&
            row.Revision == (attached ? checked(revision + 1) : revision) &&
            (attached ? row.RequestId == principal.GetTokenId() : row.RequestId is null) && binding.Matches(principal) && BrowserLoginBinding.Read(request, out var current) == BrowserLoginBindingState.Present &&
            current!.Intent == binding.Intent && current.Completion == binding.Completion && row.CompletionDigest == completionDigest &&
            principal.GetExpirationDate() is { } expires &&
            expires.ToUnixTimeSeconds() <= row.ExpiresAt && expires.ToUnixTimeSeconds() <= row.SessionExpiresAt &&
            expires > scope.Owner.Clock.GetUtcNow() && IssuerContinuationPolicy.IsCurrent(row, scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds());
    }

    /// <summary>Freezes the original browser, interaction, account, session and absolute deadlines across native persistence callbacks.</summary>
    private static string Provenance(IssuerContinuation value) => FactorBinding.Digest(new
    {
        value.IntentDigest,
        value.BrowserDigest,
        value.ClientId,
        value.CreatedAt,
        value.ExpiresAt,
        value.RetainUntil,
        value.CorrelationId,
        value.InteractionId,
        value.InteractionBrowserDigest,
        value.BrowserBudgetId,
        value.UserId,
        value.SessionId,
        value.SessionExpiresAt,
        value.CompletionDigest
    });

    /// <summary>Rejects foreign, disposed or completed native epochs and requires the original owning scope.</summary>
    private void Require(CancellationToken token)
    {
        scope.Owner.Check(phase, token);
        if (!ReferenceEquals(ProtocolStorageAmbient.Current?.Scope, scope))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
