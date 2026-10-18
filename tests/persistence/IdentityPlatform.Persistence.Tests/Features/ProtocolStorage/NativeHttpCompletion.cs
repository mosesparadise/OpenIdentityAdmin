using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Owns real framework input completion in the synthetic HTTP host, including failures before the operation starts.</summary>
internal sealed class NativeHttpCompletion : HttpResponseFeature
{
    private readonly List<(Func<object, Task> Callback, object State)> callbacks = [];

    /// <summary>Retains callbacks registered with the original host before any owned response capture exists.</summary>
    public override void OnCompleted(Func<object, Task> callback, object state) => callbacks.Add((callback, state));

    /// <summary>Executes every host callback in reverse registration order after the owned boundary has fully drained.</summary>
    internal async Task CompleteAsync()
    {
        List<Exception> failures = [];
        foreach (var (callback, state) in callbacks.AsEnumerable().Reverse())
        {
            try
            {
                await callback(state);
            }
            catch (Exception error) { failures.Add(error); }
        }
        callbacks.Clear();
        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }
}
