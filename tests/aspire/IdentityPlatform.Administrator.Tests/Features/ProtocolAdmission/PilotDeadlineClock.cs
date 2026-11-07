namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Controls only monotonic observations for exact boundary equality; UTC and watchdog timers retain real system behavior.</summary>
internal sealed class PilotDeadlineClock : TimeProvider
{
    private long ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Returns the deliberately controlled monotonic timeline without altering native token or cookie UTC lifetimes.</summary>
    public override long GetTimestamp() => Interlocked.Read(ref ticks);

    /// <summary>Advances once to the selected absolute observation; a previous request's timestamp is never reset for recovery.</summary>
    internal void AdvanceTo(int seconds)
    {
        var next = TimeSpan.FromSeconds(seconds).Ticks;
        Assert.True(next > Interlocked.Read(ref ticks));
        Interlocked.Exchange(ref ticks, next);
    }
}
