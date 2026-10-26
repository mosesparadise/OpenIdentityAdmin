using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Replaces only network I/O, preserving native serialization and issuer handling without claiming TLS interoperability.</summary>
internal sealed class NativeClientIssuerBridge(AdministratorProtocolScenario issuer, IServiceProvider? protocolServices = null) : DelegatingHandler
{
    private const int ExpiryAdvanceSeconds = 1;
    internal NativeIdentityResponse.Fault? IdentityFault
    {
        get; set;
    }
    internal bool RewriteBrowserIntent
    {
        get; set;
    }
    internal int IdentityRewrites
    {
        get; private set;
    }
    internal int ParSends
    {
        get; private set;
    }
    internal int TokenSends
    {
        get; private set;
    }
    internal int MetadataSends
    {
        get; private set;
    }

    /// <summary>Starts a separate server execution context so client ownership/transport capabilities cannot flow into the issuer.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Task<HttpResponseMessage> pending;
        using (ExecutionContext.SuppressFlow())
        {
            pending = Task.Run(() => DispatchAsync(request, cancellationToken), cancellationToken);
        }
        return pending;
    }

    /// <summary>Forwards exact synthetic native form/query bytes to the real issuer, retaining only finite send counts.</summary>
    private async Task<HttpResponseMessage> DispatchAsync(HttpRequestMessage request, CancellationToken token)
    {
        Assert.True(request.RequestUri?.GetLeftPart(UriPartial.Authority) == issuer.Issuer.GetLeftPart(UriPartial.Authority));
        var path = request.RequestUri!.AbsolutePath;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (request.Method == HttpMethod.Post)
        {
            using var reader = new FormReader(await request.Content!.ReadAsStringAsync(token));
            foreach (var pair in await reader.ReadFormAsync(token))
            {
                Assert.Equal(1, pair.Value.Count);
                fields.Add(pair.Key, pair.Value[0]!);
            }
        }
        else
        {
            Assert.Equal(HttpMethod.Get, request.Method);
        }
        Count(path);
        var result = await NativeAccountHttp.InvokeAsync(issuer, path, fields, cancellation: token, services: protocolServices);
        Assert.True(result.Failure is null, result.Failure?.GetType().FullName + Environment.NewLine + result.Failure?.StackTrace);
        var response = new HttpResponseMessage((HttpStatusCode)result.Status) { Content = new ByteArrayContent(RewriteIdentity(path, result)) };
        Assert.True(MediaTypeHeaderValue.TryParse(result.ContentType, out var contentType));
        response.Content.Headers.ContentType = contentType;
        return response;
    }

    /// <summary>Intervenes only after successful real token issuance, with an explicit one-second fixture advance for strict expiry equality.</summary>
    private byte[] RewriteIdentity(string path, NativeAccountResult result)
    {
        if (path != AdministratorProtocolScenario.TokenPath || IdentityFault is null && !RewriteBrowserIntent)
        {
            return result.Body;
        }
        Assert.Equal((int)HttpStatusCode.OK, result.Status);
        var fault = IdentityFault ?? NativeIdentityResponse.Fault.Unchanged;
        var clock = issuer.Account.Journey.Clock;
        if (fault is NativeIdentityResponse.Fault.Expiry or NativeIdentityResponse.Fault.BrowserExpiryEquality)
        {
            clock.Now = DateTimeOffset.FromUnixTimeSeconds(clock.Now.ToUnixTimeSeconds() + ExpiryAdvanceSeconds);
        }
        var body = NativeIdentityResponse.Rewrite(result.Body, issuer.Services.GetRequiredService<IssuerCredentials>().Signing, fault, clock.Now, RewriteBrowserIntent);
        IdentityRewrites++;
        return body;
    }

    /// <summary>Separates proof-bearing operations from public discovery reads without recording credentials or URLs.</summary>
    private void Count(string path)
    {
        if (path == AdministratorProtocolScenario.ParPath)
        {
            ParSends++;
        }
        else if (path == AdministratorProtocolScenario.TokenPath)
        {
            TokenSends++;
        }
        else
        {
            MetadataSends++;
        }
    }
}
