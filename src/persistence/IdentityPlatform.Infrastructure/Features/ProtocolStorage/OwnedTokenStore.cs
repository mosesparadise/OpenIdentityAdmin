using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Forwards native token persistence through explicit scoped ownership without altering manager semantics.</summary>
internal sealed class OwnedTokenStore(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options, ProtocolStorageOwner owner)
    : OpenIddictEntityFrameworkCoreTokenStore(cache, new ProtocolStorageContext(context, owner), options)
{
    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync(cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreToken>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync<TResult>(query, cancellation), cancellationToken);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask CreateAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CreateAsync(token, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask DeleteAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.DeleteAsync(token, cancellation), cancellationToken, write: true);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> FindAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindAsync(subject, client, status, type, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByApplicationIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> FindByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByAuthorizationIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByReferenceIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindBySubjectAsync(subject, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<string?> GetApplicationIdAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.GetApplicationIdAsync(token, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) where TResult : default =>
        owner.RunAsync(cancellation => base.GetAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<string?> GetAuthorizationIdAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.GetAuthorizationIdAsync(token, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> ListAsync(int? count, int? offset, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync(count, offset, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Rejects broad native pruning before it can start unbounded batches or independent transactions.</summary>
    public override ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
        owner.RejectPrune();

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.RevokeAsync(subject, client, status, type, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.RevokeByApplicationIdAsync(identifier, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.RevokeByAuthorizationIdAsync(identifier, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.RevokeBySubjectAsync(subject, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask SetApplicationIdAsync(OpenIddictEntityFrameworkCoreToken token, string? identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.SetApplicationIdAsync(token, identifier, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask SetAuthorizationIdAsync(OpenIddictEntityFrameworkCoreToken token, string? identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.SetAuthorizationIdAsync(token, identifier, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask UpdateAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.UpdateAsync(token, cancellation), cancellationToken, write: true);
}
