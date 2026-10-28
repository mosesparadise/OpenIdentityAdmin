using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Observes real client authentication without bypassing generic-token or endpoint validation.</summary>
internal sealed class CleanupAssertionProbe
{
    internal int Reached
    {
        get; private set;
    }
    internal DurableCleanupClientAssertion? Receipt
    {
        get; private set;
    }
    internal Action<OpenIddictServerEvents.ProcessAuthenticationContext>? BeforeDispatch
    {
        get; init;
    }
    internal Func<OpenIddictServerEvents.ProcessAuthenticationContext, CancellationToken, ValueTask>? BeforeTarget
    {
        get; init;
    }
    internal Func<OpenIddictServerEvents.ProcessAuthenticationContext, CancellationToken, ValueTask>? AfterTarget
    {
        get; init;
    }

    /// <summary>Records native proof and the cleanup-only receipt immediately after completed durable consumption.</summary>
    internal static async ValueTask ObserveAsync(OpenIddictServerEvents.ProcessAuthenticationContext context)
    {
        var probe = context.Transaction.GetProperty<CleanupAssertionProbe>(typeof(CleanupAssertionProbe).FullName!);
        if (probe is null)
        {
            return;
        }
        probe.Reached++;
        Assert.False(context.IsRejected);
        Assert.NotNull(context.ClientAssertionPrincipal);
        Assert.Null(context.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!));
        probe.Receipt = context.Transaction.GetProperty<DurableCleanupClientAssertion>(typeof(DurableCleanupClientAssertion).FullName!);
        Assert.NotNull(probe.Receipt);
        if (probe.BeforeTarget is not null)
        {
            await probe.BeforeTarget(context, context.CancellationToken);
        }
    }
}

/// <summary>Controls one reached actual child-scope disposal failure without changing provider commit behavior.</summary>
internal sealed class CleanupAssertionDisposalFault
{
    internal Exception? Next
    {
        get; set;
    }
    internal Action? BeforeDispose
    {
        get; set;
    }
    internal int Reached
    {
        get; set;
    }
}

/// <summary>Is constructed in each actual DbContext scope and fails only the first disposal armed after a real child commit.</summary>
internal sealed class CleanupAssertionDisposal(CleanupAssertionDisposalFault fault) : IAsyncDisposable
{
    /// <summary>Preserves the exact fault instance and consumes it once so parent disposal cannot masquerade as the child observation.</summary>
    public ValueTask DisposeAsync()
    {
        if (fault.BeforeDispose is { } action)
        {
            fault.BeforeDispose = null;
            fault.Reached++;
            action();
        }
        if (fault.Next is { } error)
        {
            fault.Next = null;
            fault.Reached++;
            return ValueTask.FromException(error);
        }
        return ValueTask.CompletedTask;
    }
}
