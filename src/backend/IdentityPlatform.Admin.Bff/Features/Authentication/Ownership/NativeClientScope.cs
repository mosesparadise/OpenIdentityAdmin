using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;

/// <summary>Owns a native service-created child through asynchronous activation and actual disposal.</summary>
internal sealed class NativeClientScope : IServiceScope, IAsyncDisposable
{
    private readonly ProtocolStorageAmbient parent;
    private readonly IDisposable lease;
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolStorageAmbient binding;
    private readonly object sync = new();
    private Task? cleanup;
    public IServiceProvider ServiceProvider => scope.Services;

    /// <summary>Captures the caller capability synchronously before the native service resolves any child service.</summary>
    internal NativeClientScope(IServiceScopeFactory factory, ProtocolStorageAmbient parent)
    {
        this.parent = parent;
        parent.Request.RequireCurrent();
        lease = parent.Scope.Owner.BeginChild();
        try
        {
            scope = ProtocolStorageScope.Create(factory, parent.Request);
            binding = new(parent.Request, scope);
            scope.Services.GetRequiredService<NativeClientActivation>().Scope = this;
        }
        catch
        {
            parent.Request.Fail();
            if (scope is not null)
            {
                ProtocolStorageQuarantine.Retain(CleanupAsync());
            }
            else
            {
                lease.Dispose();
            }
            throw;
        }
    }

    /// <summary>Publishes the new child only in the native service's current execution context.</summary>
    internal void Enter() => ProtocolStorageAmbient.Current = binding;

    /// <summary>Awaits real native context acquisition before the installed native factory can create a transaction.</summary>
    internal Task ActivateAsync() => scope.ActivateAsync(parent.Request, parent.Scope.Owner.RequestToken);

    /// <summary>Rejects unsupported synchronous disposal while retaining real cleanup in the original request graph.</summary>
    public void Dispose()
    {
        parent.Request.Fail();
        ProtocolStorageQuarantine.Retain(DisposeAsync().AsTask());
        throw ProtocolStorageOwner.Unavailable();
    }

    /// <summary>Restores the calling capability synchronously and awaits actual cleanup before releasing parent services.</summary>
    public ValueTask DisposeAsync()
    {
        if (scope.Owner.Dispatches.IsCurrentFlow)
        {
            parent.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        if (ReferenceEquals(ProtocolStorageAmbient.Current, binding))
        {
            ProtocolStorageAmbient.Current = parent;
        }
        lock (sync)
        {
            return new(cleanup ??= CleanupAsync());
        }
    }

    /// <summary>Joins quarantine completion as well as the bounded disposal result.</summary>
    private async Task CleanupAsync()
    {
        try
        {
            await scope.DisposeAsync();
        }
        catch { parent.Request.Fail(); throw; }
        finally
        {
            try
            {
                await scope.ActualCompletion;
            }
            finally { lease.Dispose(); }
        }
    }
}
