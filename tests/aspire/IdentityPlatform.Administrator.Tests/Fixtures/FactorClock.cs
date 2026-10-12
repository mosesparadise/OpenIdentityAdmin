namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Controls only product expiry checks; the real native authenticator still uses its production clock.</summary>
internal sealed class FactorClock(DateTimeOffset now) : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = now;

    /// <summary>Returns the explicit policy boundary without changing the independent native OTP oracle.</summary>
    public override DateTimeOffset GetUtcNow() => Now;
}
