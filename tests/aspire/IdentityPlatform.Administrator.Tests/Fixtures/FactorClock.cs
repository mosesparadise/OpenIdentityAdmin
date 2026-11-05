namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Shares product and native protocol time; defaults to explicit boundaries while real browser journeys can follow system UTC. Native OTP retains its independent production clock.</summary>
internal sealed class FactorClock : TimeProvider
{
    private const string ReadOnlyClock = "A source-backed fixture clock cannot be manually reset.";
    private readonly TimeProvider? source;
    private DateTimeOffset now;

    /// <summary>Preserves the existing manually controlled clock for deterministic policy-boundary tests.</summary>
    internal FactorClock(DateTimeOffset now) => this.now = now;

    /// <summary>Follows one native time source from fixture creation without replacing inherited real timers.</summary>
    internal FactorClock(TimeProvider source) => this.source = source ?? throw new ArgumentNullException(nameof(source));

    internal DateTimeOffset Now
    {
        /// <summary>Observes the selected source on every read, or retains the explicit frozen boundary.</summary>
        get => source?.GetUtcNow() ?? now;
        /// <summary>Allows boundary changes only in the original frozen mode; live observations cannot renew captured deadlines.</summary>
        set
        {
            if (source is not null)
            {
                throw new InvalidOperationException(ReadOnlyClock);
            }
            now = value;
        }
    }

    /// <summary>Returns the shared current protocol observation without changing the independent native OTP oracle.</summary>
    public override DateTimeOffset GetUtcNow() => Now;
}
