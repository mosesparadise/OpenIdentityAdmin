using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves actual graceful client-process restart and protected-key reimport across a genuine native HTTPS exchange and replay.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientProcessTests(BootstrapFixture fixture)
{
    private const string Replay = "InvalidToken/ID2139";
    private const string CookieSeparator = "; ";
    private const int IssuedRows = 2;
    private const string DuplicateProvider = ",\"Provider\":";
    private const string ObjectEnd = "}";
    private const string IdentifierFormat = "N";
    private const string TrailingFrame = "\n{}";
    private const string MissingKeyDirectory = "native-client-absent-";
    private const string MissingKeyFile = "absent.pfx";
    private const string CredentialPrefix = "ClientCredentials:";
    private const string PathSuffix = ":Path";
    private const string PasswordSuffix = ":Password";
    private const string SyntheticPassword = "test-only-absent-file";
    private const string UnusedConnection = "not-used-before-key-load";
    private const string LocalIssuer = "https://localhost/";
    private const string Client = "https://administrator.example.test/";
    private const string Callback = "https://administrator.example.test/callback";
    private const string KeyId = "absent-key-control";
    private static readonly string[] CredentialRoles = ["StateSigning", "StateEncryption", "AssertionSigning"];

    /// <summary>Malformed private frames must fail before native composition, with bounded fixed output and complete process-group cleanup.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_InvalidPrivateFrames_When_ClientProcessStarts_Then_InputFailureNeverReachesNativeServices(string provider)
    {
        var frame = JsonSerializer.Serialize(MissingKeys(provider));
        await NativeClientProcess.RequireFailureAsync(Encoding.UTF8.GetBytes(frame), NativeClientProcessFailureStage.Request);
        var duplicate = frame[..^1] + DuplicateProvider + JsonSerializer.Serialize(provider) + ObjectEnd;
        await NativeClientProcess.RequireFailureAsync(Encoding.UTF8.GetBytes(duplicate), NativeClientProcessFailureStage.Input);
        await NativeClientProcess.RequireFailureAsync(Encoding.UTF8.GetBytes(frame + TrailingFrame), NativeClientProcessFailureStage.Input);
    }

    /// <summary>Builds an otherwise accepted frame whose absent key files stop composition before any persistence registration or access.</summary>
    private static NativeClientProcessInput MissingKeys(string provider)
    {
        _ = DatabaseProviderParser.Parse(provider);
        var directory = Path.Combine(Path.GetTempPath(), MissingKeyDirectory + Guid.NewGuid().ToString(IdentifierFormat));
        var path = Path.Combine(directory, MissingKeyFile);
        Assert.False(Directory.Exists(directory));
        Assert.False(File.Exists(path));
        var credentials = new Dictionary<string, string?>();
        foreach (var role in CredentialRoles)
        {
            credentials.Add(CredentialPrefix + role + PathSuffix, path);
            credentials.Add(CredentialPrefix + role + PasswordSuffix, SyntheticPassword);
        }
        return new()
        {
            Provider = provider,
            RuntimeConnection = UnusedConnection,
            Credentials = credentials,
            Issuer = LocalIssuer,
            Client = Client,
            Callback = Callback,
            KeyId = KeyId,
            UtcMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>Every predecessor process has fully exited before the next imports keys and resolves the same persisted native state.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ChallengeProcessHasExited_When_NewProcessesCompleteAndReplayCallback_Then_ExactlyOneGenuineExchangeSurvivesRestart(string provider)
    {
        using var material = new NativeClientCredentialFixture();
        using var keys = NativeClientInteroperabilityTests.Load(material);
        await using var host = await NativeIssuerHttpsHost.StartAsync();
        var profile = NativeClientInteroperabilityTests.Profile(keys, host.Issuer);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration, host.Issuer);
        host.Bind(issuer);
        try
        {
            await using var database = await fixture.LeaseSessionsAsync(provider);
            await using var observer = new NativeClientHttp(database, issuer, profile, keys);
            var first = await NativeClientProcess.RunAsync(Input(provider, database, material, profile, issuer));
            await host.DrainAsync();
            var challenge = Challenge(first.Reply);
            await NativeClientInteroperabilityTests.RequireStatesAsync(observer, total: 1, redeemed: 0);
            var callback = await NativeClientInteroperabilityTests.AuthorizeAsync(issuer, challenge, cookie: true);
            var before = await NativeAccountPersistence.CountAsync(issuer);
            var second = await NativeClientProcess.RunAsync(Input(provider, database, material, profile, issuer, callback, challenge.Cookie));
            await host.DrainAsync();
            RequireAccepted(second.Reply, issuer);
            Assert.Equal((before.Tokens + IssuedRows, before.Authorizations), await NativeAccountPersistence.CountAsync(issuer));
            await NativeClientInteroperabilityTests.RequireStatesAsync(observer, total: 1, redeemed: 1);
            var after = await NativeAccountPersistence.CountAsync(issuer);
            var third = await NativeClientProcess.RunAsync(Input(provider, database, material, profile, issuer, callback, challenge.Cookie));
            await host.DrainAsync();
            RequireReplay(third.Reply);
            Assert.Equal(after, await NativeAccountPersistence.CountAsync(issuer));
            await NativeClientInteroperabilityTests.RequireStatesAsync(observer, total: 1, redeemed: 1);
            Assert.Equal(1, host.ParSends);
            Assert.Equal(1, host.TokenSends);
            Assert.Equal(0, host.Failures);
            Assert.Equal(0, host.RedirectedSends);
            Assert.True(first.Generation != second.Generation && second.Generation != third.Generation && first.Generation != third.Generation);
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Supplies only runtime Sessions access and explicit synthetic credentials/profile/time through the private parent pipe.</summary>
    private static NativeClientProcessInput Input(string provider, BootstrapSessions database, NativeClientCredentialFixture material,
        NativeClientProfile profile, AdministratorProtocolScenario issuer, Uri? callback = null, string? cookie = null) => new()
        {
            Provider = provider,
            RuntimeConnection = database.RuntimeConnection,
            Credentials = material.Settings(),
            Issuer = profile.Issuer.AbsoluteUri,
            Client = profile.ClientUri.AbsoluteUri,
            Callback = profile.Callback.AbsoluteUri,
            KeyId = profile.Registration.Key.KeyId,
            UtcMilliseconds = issuer.Account.Journey.Clock.GetUtcNow().ToUnixTimeMilliseconds(),
            RequestUri = callback?.AbsoluteUri,
            Cookie = cookie
        };

    /// <summary>Requires a genuine native challenge projection without manufacturing an authentication result or exposing cookie values.</summary>
    private static NativeClientHttpResult Challenge(NativeClientProcessReply reply)
    {
        Assert.Equal(StatusCodes.Status302Found, reply.Status);
        Assert.False(reply.Aborted);
        Assert.False(reply.Authenticated);
        Assert.Equal(0, reply.BodyLength);
        Assert.True(reply.Cookies.Length == 1);
        var challenge = new NativeClientHttpResult
        {
            Status = reply.Status,
            Location = reply.Location,
            Cookies = reply.Cookies,
            Cookie = string.Join(CookieSeparator, reply.Cookies.Select(value => value.Split(';')[0]))
        };
        NativeClientInteroperabilityTests.RequireChallenge(challenge);
        return challenge;
    }

    /// <summary>Checks the driver's projection of actual native authentication, not a parent-created ticket or principal.</summary>
    private static void RequireAccepted(NativeClientProcessReply reply, AdministratorProtocolScenario issuer)
    {
        Assert.Equal(StatusCodes.Status200OK, reply.Status);
        Assert.False(reply.Aborted);
        Assert.True(reply.Authenticated);
        Assert.True(reply.Subject == issuer.Account.ActorId);
        Assert.True(reply.Location.Length == 0);
        Assert.Equal(0, reply.BodyLength);
        foreach (var cookie in reply.Cookies)
        {
            NativeClientInteroperabilityTests.RequireCookie(cookie, attributes: false);
        }
    }

    /// <summary>Requires exact redeemed-state rejection and no private publication from the third fresh OS process.</summary>
    private static void RequireReplay(NativeClientProcessReply reply)
    {
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, reply.Status);
        Assert.False(reply.Aborted);
        Assert.False(reply.Authenticated);
        Assert.Equal(Replay, reply.Rejection);
        Assert.True(reply.Location.Length == 0);
        Assert.True(reply.Cookies.Length == 0);
        Assert.Equal(0, reply.BodyLength);
    }
}
