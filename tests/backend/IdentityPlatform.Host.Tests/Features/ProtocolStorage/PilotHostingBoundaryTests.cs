using IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Preserves standalone account, authorization and discovery lifetimes when token-only coordination is enabled.</summary>
public sealed class PilotHostingBoundaryTests
{
    private const string Authorize = "/authorize";
    private const string Token = "/token";
    private const string Host = "issuer.example.test";

    /// <summary>Actual native endpoint inference leaves every non-token delegate outside the pilot token watchdog.</summary>
    [Theory]
    [InlineData("/account/login")]
    [InlineData(Authorize)]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/unknown")]
    public async Task Given_NonTokenStandaloneRoute_When_PilotBoundaryInvokesNext_Then_NoTokenDeadlineIsInstalled(string path)
    {
        var services = new ServiceCollection().AddLogging();
        var clock = new TimerObservation();
        services.AddSingleton<TimeProvider>(clock);
        services.AddOpenIddict().AddServer().AllowAuthorizationCodeFlow().SetAuthorizationEndpointUris(Authorize)
            .SetTokenEndpointUris(Token).AddEphemeralEncryptionKey().AddEphemeralSigningKey();
        await using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Host = new HostString(Host);
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Path = path;
        var invoked = false;
        await PilotHttpBoundary.InvokeAsync(http, _ => { invoked = true; return Task.CompletedTask; });
        Assert.True(invoked);
        Assert.Equal(0, clock.Timers);
    }

    /// <summary>Observes timer creation without advancing or replacing the actual monotonic clock.</summary>
    private sealed class TimerObservation : TimeProvider
    {
        internal int Timers
        {
            get; private set;
        }

        /// <summary>Counts any accidentally installed token watchdog while retaining normal timer behavior.</summary>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Timers++;
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }
}
