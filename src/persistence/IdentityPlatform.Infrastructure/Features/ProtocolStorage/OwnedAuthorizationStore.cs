using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Forwards native authorization persistence through explicit scoped ownership without altering manager semantics.</summary>
internal sealed class OwnedAuthorizationStore(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options, ProtocolStorageOwner owner)
    : OpenIddictEntityFrameworkCoreAuthorizationStore(cache, new ProtocolStorageContext(context, owner), options)
{
    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync(cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync<TResult>(query, cancellation), cancellationToken);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask CreateAsync(OpenIddictEntityFrameworkCoreAuthorization authorization, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CreateAsync(authorization, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask DeleteAsync(OpenIddictEntityFrameworkCoreAuthorization authorization, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.DeleteAsync(authorization, cancellation), cancellationToken, write: true);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreAuthorization> FindAsync(string? subject, string? client, string? status, string? type, ImmutableArray<string>? scopes, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindAsync(subject, client, status, type, scopes, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreAuthorization> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByApplicationIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreAuthorization> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindBySubjectAsync(subject, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<string?> GetApplicationIdAsync(OpenIddictEntityFrameworkCoreAuthorization authorization, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.GetApplicationIdAsync(authorization, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreAuthorization>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) where TResult : default =>
        owner.RunAsync(cancellation => base.GetAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreAuthorization> ListAsync(int? count, int? offset, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync(count, offset, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreAuthorization>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
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
    public override ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.RevokeBySubjectAsync(subject, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask SetApplicationIdAsync(OpenIddictEntityFrameworkCoreAuthorization authorization, string? identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.SetApplicationIdAsync(authorization, identifier, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask UpdateAsync(OpenIddictEntityFrameworkCoreAuthorization authorization, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.UpdateAsync(authorization, cancellation), cancellationToken, write: true);
}
