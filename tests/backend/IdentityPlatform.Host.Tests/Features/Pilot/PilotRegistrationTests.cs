using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.Pilot;

public sealed partial class PilotRegistrationTests
{
    [Fact]
    public void Native_protocol_options_cannot_advertise_unimplemented_flows_or_weaker_challenges()
    {
        using var fixture = new IssuerMaterialFixture();
        var services = new ServiceCollection().AddLogging();
        services.AddIdentityPersistence(DatabaseProvider.PostgreSql, "Host=localhost;Database=synthetic");
        using var credentials = services.AddIssuerProtection(fixture.Configuration)!;
        services.AddFirstPartyPilot(credentials);
        using var provider = services.BuildServiceProvider();
        var protocol = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        Assert.Equal(credentials.Issuer, protocol.Issuer);
        Assert.Equal(["authorization_code"], protocol.GrantTypes);
        Assert.Equal(["code"], protocol.ResponseTypes);
        Assert.Equal(["query"], protocol.ResponseModes);
        Assert.Equal(["S256"], protocol.CodeChallengeMethods);
        Assert.Equal(["client_secret_basic"], protocol.ClientAuthenticationMethods);
        Assert.True(protocol.RequireProofKeyForCodeExchange);
        Assert.Equal(TimeSpan.FromSeconds(60), protocol.AuthorizationCodeLifetime);
        Assert.Equal(TimeSpan.FromMinutes(5), protocol.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromMinutes(5), protocol.IdentityTokenLifetime);
        Assert.Equal(TimeSpan.FromMinutes(5), protocol.RequestTokenLifetime);
        Assert.Equal([new Uri("/connect/authorize", UriKind.Relative)], protocol.AuthorizationEndpointUris);
        Assert.Equal([new Uri("/connect/token", UriKind.Relative)], protocol.TokenEndpointUris);
        Assert.Empty(protocol.UserInfoEndpointUris);
        Assert.Empty(protocol.EndSessionEndpointUris);
        Assert.Empty(protocol.PushedAuthorizationEndpointUris);
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        Assert.Equal(PilotProfile.SessionCookie, cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.Equal("/", cookie.Cookie.Path);
        Assert.Null(cookie.Cookie.Domain);
        Assert.False(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(15), cookie.ExpireTimeSpan);
        Assert.Equal(IdentityConstants.ApplicationScheme, provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme);
        var antiforgery = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;
        Assert.StartsWith("__Host-", antiforgery.Cookie.Name);
        Assert.Equal(CookieSecurePolicy.Always, antiforgery.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, antiforgery.Cookie.SameSite);
        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(16, identity.Password.RequiredLength);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), identity.Lockout.DefaultLockoutTimeSpan);
        Assert.Equal(210_000, provider.GetRequiredService<IOptions<PasswordHasherOptions>>().Value.IterationCount);
    }

    [Fact]
    public void Diagnostic_overrides_and_options_reload_cannot_expose_protocol_secrets()
    {
        var messages = new List<string>();
        using var sink = new CaptureProvider(messages);
        var services = new ServiceCollection();
        services.AddLogging(logs => logs.SetMinimumLevel(LogLevel.Trace).AddProvider(sink)
            .AddFilter<CaptureProvider>("OpenIddict.Server", LogLevel.Trace)
            .AddFilter<CaptureProvider>("Microsoft.AspNetCore", LogLevel.Trace)
            .AddFilter<CaptureProvider>("Microsoft.EntityFrameworkCore", LogLevel.Trace));
        PilotDiagnostics.SuppressProtocolSecrets(services);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();
        AssertLogs();
        provider.GetRequiredService<IOptionsMonitorCache<LoggerFilterOptions>>().Clear();
        // A rebuilt option graph must apply the post-configuration guard again.
        var rebuilt = provider.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>().CurrentValue;
        Assert.Contains(rebuilt.Rules, rule => rule.CategoryName == "OpenIddict" && rule.LogLevel == LogLevel.None);
        AssertLogs();
        Assert.Equal(2, messages.Count);
        Assert.True(messages.All(value => value == "Safe operational event"), "Unexpected diagnostic content.");

        void AssertLogs()
        {
            foreach (var category in new[] { "OpenIddict.Server", "Microsoft.AspNetCore.Hosting.Diagnostics", "Microsoft.EntityFrameworkCore.Database.Command" })
            {
                SecretEvent(factory.CreateLogger(category), "synthetic-private-sentinel");
            }
            var operational = factory.CreateLogger("IdentityPlatform.Operations");
            SafeEvent(operational);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "{Sentinel}")]
    private static partial void SecretEvent(ILogger logger, string sentinel);
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Safe operational event")]
    private static partial void SafeEvent(ILogger logger);

    private sealed class CaptureProvider(List<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => messages.Add(formatter(state, exception));
    }
}
