using System.Net;
using System.Security.Cryptography;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Preserves actual browser authority and replay/factor storage across replacement of the real AuthServer executable.</summary>
[Collection("administrator-bootstrap")]
public sealed class ProcessRestartTests(BootstrapFixture fixture)
{
    private const string FilePattern = "*";

    /// <summary>Original cookies remain valid while a still-native-valid used OTP stays denied and fresh proof/recovery remains usable.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_EnrolledAdministrator_When_AuthServerProcessRestarts_Then_CookieFactorAndReplayContinuityUseOriginalDurableState(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        var originalCookie = cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value;
        var material = Snapshot(environment);
        await environment.RestartPrimaryAsync();
        var retained = Snapshot(environment);
        Assert.True(material.Count == retained.Count && material.All(file => retained.TryGetValue(file.Key, out var hash) && hash == file.Value));
        Assert.True(originalCookie == cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value);
        using var ready = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using var stranger = environment.Browser(new CookieContainer());
        var location = await AdministratorJourney.BeginAsync(environment, stranger, enrollment: false);
        var form = await AdministratorJourney.ReadAsync(stranger, location, enrollment: false);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var replay = await AdministratorJourney.ConfirmAsync(stranger, form, enrolled.AcceptedCode, enrollment: false);
        FactorOracle.RequireWindow(enrolled.Seed, enrolled.AcceptedCode, started, valid: true);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var fresh = await AdministratorJourney.ConfirmAsync(stranger, form, FactorOracle.UnusedCode(enrolled.Seed, enrolled.AcceptedCode), enrollment: false);
        Assert.Equal(HttpStatusCode.Redirect, fresh.StatusCode);
        using var recovery = environment.Browser(new CookieContainer(), secondary: true);
        var recoveryLocation = await AdministratorJourney.BeginAtAsync(environment, recovery, SecurityHttp.Recovery);
        var recoveryForm = await AdministratorJourney.ReadAsync(recovery, recoveryLocation, enrollment: false);
        _ = await SecurityHttp.TransitionAsync(recovery, recoveryForm, enrolled.RecoveryCodes[0], recovery: true);
    }

    /// <summary>Compares original populated protection files without printing paths, contents or secret-derived fingerprints.</summary>
    private static Dictionary<string, string> Snapshot(AdministratorEnvironment environment) => Directory.GetFiles(environment.Material.Root, FilePattern, SearchOption.AllDirectories)
        .ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
}
