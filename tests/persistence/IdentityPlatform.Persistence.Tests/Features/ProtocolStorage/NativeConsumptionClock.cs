namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Controls only proof wall time while leaving original request/transaction timers on real monotonic time.</summary>
internal sealed class NativeConsumptionClock : TimeProvider
{
    private DateTimeOffset? now;

    /// <summary>Returns a fixed proof time only while a boundary scenario is active.</summary>
    public override DateTimeOffset GetUtcNow() => now ?? TimeProvider.System.GetUtcNow();

    /// <summary>Chooses an exact UTC-second boundary independently of native claim serialization.</summary>
    internal DateTimeOffset Freeze()
    {
        now = DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds());
        return now.Value;
    }

    /// <summary>Moves proof wall time after a real database wait without renewing monotonic request time.</summary>
    internal void Set(DateTimeOffset value) => now = value;

    /// <summary>Restores ordinary proof wall time after each isolated scenario.</summary>
    internal void Reset() => now = null;
}
