using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves closed B3 private process input before credential import or database access, using actual exited child processes.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProcessFramingTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Application = "IdentityPlatform.Test.BrowserProcess";
    private const string Unknown = "Unexpected";
    private const string Token = "synthetic-unused-token";
    private const string PrivateMarker = "synthetic-private-output-marker";
    private const string AnonymousStatus = "{\"authenticated\":false}";
    private const string Callback = "https://administrator.example.test/callback";
    private const string Prefix = "Bff:Sessions:Protection:";
    private const string ApplicationKey = Prefix + "ApplicationId";
    private const string RingKey = Prefix + "KeyRingPath";
    private const string WrapperPathKey = Prefix + "Current:Path";
    private const string WrapperPasswordKey = Prefix + "Current:Password";
    private const string CredentialPath = "ClientCredentials:StateSigning:Path";
    private const string CredentialPassword = "ClientCredentials:StateSigning:Password";

    /// <summary>Valid framing reaches the absent-key control; each mixed, unknown, missing or ambiguous field instead fails at Input without readiness.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ClosedBrowserProcessOperations_When_PrivateFieldsAreMixedOrInvalid_Then_InputIsRejectedBeforeNativeServices(string provider)
    {
        RequireSafeFailures();
        var input = NativeClientProcessTests.MissingKeys(provider);
        var frame = JsonSerializer.SerializeToNode(input)!.AsObject();
        frame[nameof(input.Issuer)] = Issuer;
        frame[nameof(input.Cookie)] = Token;
        frame[nameof(input.Browser)] = JsonSerializer.SerializeToNode(new BrowserProcessRequest
        {
            Operation = BrowserProcessOperation.Status,
            UtcTicks = DateTimeOffset.FromUnixTimeMilliseconds(input.UtcMilliseconds).UtcTicks,
            Protection = new()
            {
                [ApplicationKey] = Application,
                [RingKey] = Path.GetDirectoryName(input.Credentials[CredentialPath]),
                [WrapperPathKey] = input.Credentials[CredentialPath],
                [WrapperPasswordKey] = input.Credentials[CredentialPassword]
            }
        });
        await NativeClientProcess.RequireFailureAsync(JsonSerializer.SerializeToUtf8Bytes(frame), NativeClientProcessFailureStage.Request);
        await RejectAsync(frame, value => value[nameof(input.RequestUri)] = Callback);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.Operation)] = 0);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.Operation)] = int.MaxValue);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.FormToken)] = Token);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.Operation)] = (int)BrowserProcessOperation.LogoutSubmit);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.UtcTicks)] = 0);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.Protection)]![Unknown] = Token);
        await RejectAsync(frame, value => Browser(value)[nameof(BrowserProcessRequest.Protection)] = null);
    }

    /// <summary>Mutates one independently cloned frame so each actual child rejection isolates its own invalid field.</summary>
    private static Task RejectAsync(JsonObject original, Action<JsonObject> alter)
    {
        var changed = original.DeepClone().AsObject();
        alter(changed);
        return NativeClientProcess.RequireFailureAsync(JsonSerializer.SerializeToUtf8Bytes(changed), NativeClientProcessFailureStage.Input);
    }

    /// <summary>Accesses only the closed nested browser request while leaving common native framing untouched.</summary>
    private static JsonObject Browser(JsonObject frame) => frame[nameof(NativeClientProcessInput.Browser)]!.AsObject();

    /// <summary>Seeds secret-bearing cookie, redirect and unexpected form regressions; fixed assertion diagnostics must not retain their private contents.</summary>
    private static void RequireSafeFailures()
    {
        var cookie = new NativeClientProcessReply { Status = (int)HttpStatusCode.OK, Location = string.Empty, Cookies = [PrivateMarker], Rejection = string.Empty, BrowserBody = AnonymousStatus };
        var redirect = new NativeClientProcessReply { Status = (int)HttpStatusCode.OK, Location = PrivateMarker, Cookies = [], Rejection = string.Empty, BrowserBody = string.Empty };
        var form = new NativeClientProcessReply { Status = (int)HttpStatusCode.OK, Location = string.Empty, Cookies = [], Rejection = string.Empty, BrowserBody = PrivateMarker };
        var failures = new[]
        {
            Assert.ThrowsAny<Exception>(() => BrowserProcessAssertions.RequireStatus(cookie, authenticated: false)),
            Assert.ThrowsAny<Exception>(() => BrowserProcessAssertions.RequireLocal(redirect, HttpStatusCode.OK)),
            Assert.ThrowsAny<Exception>(() => BrowserProcessAssertions.RequireCommitted(form))
        };
        Assert.All(failures, failure => Assert.False(failure.ToString().Contains(PrivateMarker, StringComparison.Ordinal)));
    }
}
