using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Captures real client diagnostics at trace level around a genuine exchange and replay while keeping all synthetic credentials private.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientLoggingTests(BootstrapFixture fixture)
{
    /// <summary>Real state, assertion, correlation, PAR, code and issued-token observations must never appear in enabled native diagnostics.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TraceDiagnosticsEnabled_When_NativeExchangeAndReplayRun_Then_ObservedCredentialsNeverEnterMessagesOrScopes(string provider)
    {
        NativeClientLogCapture.RequireDetector();
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        var profile = NativeClientInteroperabilityTests.Profile(keys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        using var logs = new NativeClientLogCapture();
        var values = new NativeClientPrivateValues();
        await using var client = new NativeClientHttp(database, issuer, profile, keys, services =>
        {
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
            values.Configure(services);
        });
        logs.RequireConnected(client.Services.GetRequiredService<ILoggerFactory>());
        var challenge = await NativeClientInteroperabilityTests.ChallengeAsync(client);
        values.ObserveChallenge(challenge);
        var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
        NativeClientInteroperabilityTests.RequireAccepted(await client.InvokeAsync(callback, challenge.Cookie));
        NativeClientDenial.RequireReplay(await client.InvokeAsync(callback, challenge.Cookie));
        Assert.Equal(1, client.Bridge.ParSends);
        Assert.Equal(1, client.Bridge.TokenSends);
        logs.RequirePrivate(values.RequireComplete());
    }
}
