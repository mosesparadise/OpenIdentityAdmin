using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Forwards native application persistence through explicit scoped ownership without altering manager semantics.</summary>
internal sealed class OwnedApplicationStore(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options, ProtocolStorageOwner owner)
    : OpenIddictEntityFrameworkCoreApplicationStore(cache, new ProtocolStorageContext(context, owner), options)
{
    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync(CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync(cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<long> CountAsync<TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CountAsync<TResult>(query, cancellation), cancellationToken);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask CreateAsync(OpenIddictEntityFrameworkCoreApplication application, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.CreateAsync(application, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask DeleteAsync(OpenIddictEntityFrameworkCoreApplication application, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.DeleteAsync(application, cancellation), cancellationToken, write: true);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByClientIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<OpenIddictEntityFrameworkCoreApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.FindByIdAsync(identifier, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreApplication> FindByPostLogoutRedirectUriAsync([StringSyntax(StringSyntaxAttribute.Uri)] string uri, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByPostLogoutRedirectUriAsync(uri, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreApplication> FindByRedirectUriAsync([StringSyntax(StringSyntaxAttribute.Uri)] string uri, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.FindByRedirectUriAsync(uri, cancellation), cancellationToken);

    /// <summary>Preserves native lookup behavior while checking original cancellation, time and exact context ownership.</summary>
    public override ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) where TResult : default =>
        owner.RunAsync(cancellation => base.GetAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreApplication> ListAsync(int? count, int? offset, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync(count, offset, cancellation), cancellationToken);

    /// <summary>Captures the current epoch immediately and guards each native reader execution through disposal.</summary>
    public override IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<OpenIddictEntityFrameworkCoreApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        owner.Enumerate(cancellation => base.ListAsync<TState, TResult>(query, state, cancellation), cancellationToken);

    /// <summary>Preserves native mutation behavior inside the exact owned explicit transaction and cancellation lifetime.</summary>
    public override ValueTask UpdateAsync(OpenIddictEntityFrameworkCoreApplication application, CancellationToken cancellationToken) =>
        owner.RunAsync(cancellation => base.UpdateAsync(application, cancellation), cancellationToken, write: true);
}
