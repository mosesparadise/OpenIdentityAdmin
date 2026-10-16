using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Forwards native scope persistence through explicit scoped ownership without altering manager semantics.</summary>
internal sealed class OwnedScopeStore(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options, ProtocolStorageOwner owner)
    : OpenIddictEntityFrameworkCoreScopeStore(cache, new ProtocolStorageContext(context, owner), options)
{
    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync(cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreScope>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync<TResult>(query, cancellation), cancellationToken);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask CreateAsync(OpenIddictEntityFrameworkCoreScope scope, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CreateAsync(scope, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask DeleteAsync(OpenIddictEntityFrameworkCoreScope scope, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.DeleteAsync(scope, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreScope?> FindByNameAsync(string name, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByNameAsync(name, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreScope> FindByNamesAsync(ImmutableArray<string> names, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByNamesAsync(names, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreScope> FindByResourceAsync(string resource, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByResourceAsync(resource, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) where TResult : default =>
        owner.RunAsync(cancellation => base.GetAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreScope> ListAsync(int? count, int? offset, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync(count, offset, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask UpdateAsync(OpenIddictEntityFrameworkCoreScope scope, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.UpdateAsync(scope, cancellation), cancellationToken, write: true);
}
