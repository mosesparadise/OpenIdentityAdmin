using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Testing;

/// <summary>Owns synthetic, isolated BFF material; never installs routes or grants administrator authority.</summary>
internal sealed class BffSessionFixture : IDisposable
{
    internal const string Subject = "synthetic-bff-subject";
    internal const string SourceSession = "synthetic-bff-session";
    internal const string TokenSentinel = "synthetic-private-token-sentinel";
    internal const string TokenName = "access_token";
    internal const string BffPrefix = "Bff:Sessions:Protection:";
    private const string IssuerProtectionPrefix = "Identity:Protection:DataProtection:";
    private const string ApplicationSetting = "ApplicationId";
    private const string ApplicationId = "synthetic-deployment";
    private const string SubjectClaim = "sub";
    private const string SessionClaim = "sid";
    internal IssuerMaterialFixture Material { get; } = new();
    internal static readonly DateTimeOffset Start = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    internal static readonly TimeSpan Idle = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan Absolute = TimeSpan.FromHours(8);

    /// <summary>Maps only synthetic wrapping/ring settings; issuer application identity is never reused.</summary>
    internal Dictionary<string, string?> Settings()
    {
        var settings = Material.Settings.Where(pair => pair.Key.StartsWith(IssuerProtectionPrefix, StringComparison.Ordinal))
            .ToDictionary(pair => BffPrefix + pair.Key[IssuerProtectionPrefix.Length..], pair => pair.Value);
        settings[BffPrefix + ApplicationSetting] = ApplicationId;
        return settings;
    }

    /// <summary>Creates an independently owned verified BFF provider from the fixture's current wrapper generations.</summary>
    internal SessionProtection Protection() => SessionProtection.Create(new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build());

    /// <summary>Constructs a canonical native ticket with synthetic token/property sentinels for round-trip assertions.</summary>
    internal static AuthenticationTicket Ticket()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SubjectClaim, Subject), new Claim(SessionClaim, SourceSession)],
            SessionTicketPolicy.Scheme));
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = TokenName, Value = TokenSentinel }]);
        return new AuthenticationTicket(principal, properties, SessionTicketPolicy.Scheme);
    }

    /// <summary>Builds valid synthetic metadata for codec-only controls without substituting for native provider evidence.</summary>
    internal static AdminSessionTicket Row(DateTimeOffset? now = null) => SessionTicketPolicy.Create(SessionHandle.Create().Digest,
        Subject, SourceSession, now ?? Start, (now ?? Start).Add(Absolute), Idle, Absolute);

    /// <summary>Removes only this fixture's synthetic files after all protection consumers have been disposed.</summary>
    public void Dispose() => Material.Dispose();
}

/// <summary>Provides deterministic tick-precise time without sleeping across expiry boundaries.</summary>
internal sealed class BffSessionClock(DateTimeOffset initial) : TimeProvider
{
    private long ticks = initial.UtcTicks;
    /// <summary>Reads the current instant atomically for concurrent store instances.</summary>
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    /// <summary>Moves synthetic time explicitly, including rollback scenarios.</summary>
    internal void Set(DateTimeOffset value) => Interlocked.Exchange(ref ticks, value.UtcTicks);
}
