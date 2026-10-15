using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Substitutes query rows and update cancellation while retaining the actual native manager and relationship getters.</summary>
internal sealed class NativeStoreProbe(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
    IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options)
    : OpenIddictEntityFrameworkCoreTokenStore(cache, context, options)
{
    internal const string ExactIdentifier = "probe-binding";
    private const string DifferentCaseIdentifier = "PROBE-BINDING";
    internal bool EnumerationActive
    {
        get; private set;
    }
    internal int GetterCalls
    {
        get; private set;
    }
    internal int Disposals
    {
        get; private set;
    }
    internal bool RejectGetterDuringEnumeration
    {
        get; set;
    }
    internal CancellationTokenSource? CancelUpdate
    {
        get; set;
    }
    internal int UpdateCalls
    {
        get; private set;
    }

    internal CancellationToken ReceivedToken
    {
        get; private set;
    }
    internal CancellationToken ThrownToken
    {
        get; private set;
    }
    internal bool CancellationThrown
    {
        get; private set;
    }

    /// <summary>Emulates a provider returning exact and case-variant relationship matches; the manager must filter them.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> FindByApplicationIdAsync(string identifier,
        CancellationToken cancellationToken) => Rows(cancellationToken);

    /// <summary>Uses the same controlled rows for the independent authorization relationship filter.</summary>
    public override IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> FindByAuthorizationIdAsync(string identifier,
        CancellationToken cancellationToken) => Rows(cancellationToken);

    /// <summary>Retains the base context lookup and loaded relationship semantics inside the manager's active enumeration.</summary>
    public override ValueTask<string?> GetApplicationIdAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken)
    {
        ObserveGetter();
        return base.GetApplicationIdAsync(token, cancellationToken);
    }

    /// <summary>Retains the native authorization getter instead of substituting the manager's ordinal filter result.</summary>
    public override ValueTask<string?> GetAuthorizationIdAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken)
    {
        ObserveGetter();
        return base.GetAuthorizationIdAsync(token, cancellationToken);
    }

    /// <summary>Fails at the actual manager-to-store write boundary; no database persistence is claimed.</summary>
    public override ValueTask UpdateAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken)
    {
        UpdateCalls++;
        ReceivedToken = cancellationToken;
        CancelUpdate!.Cancel();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException exception)
        {
            CancellationThrown = true;
            ThrownToken = exception.CancellationToken;
            throw;
        }
        throw new InvalidOperationException();
    }

    /// <summary>Models a reader lifetime independently of the sequential calls made between yielded rows.</summary>
    private async IAsyncEnumerable<OpenIddictEntityFrameworkCoreToken> Rows([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnumerationActive = true;
        try
        {
            await Task.Yield();
            foreach (var identifier in new[] { ExactIdentifier, DifferentCaseIdentifier })
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new OpenIddictEntityFrameworkCoreToken
                {
                    Application = new() { Id = identifier },
                    Authorization = new() { Id = identifier }
                };
            }
        }
        finally { EnumerationActive = false; Disposals++; }
    }

    /// <summary>The seeded invalid whole-enumerator lock must reject the genuine native filtering pattern.</summary>
    private void ObserveGetter()
    {
        Assert.True(EnumerationActive);
        GetterCalls++;
        if (RejectGetterDuringEnumeration)
        {
            throw new InvalidOperationException();
        }
    }
}
