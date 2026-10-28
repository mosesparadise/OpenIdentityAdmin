using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Invokes the actual native revocation service and signing pipeline; only network I/O is replaced by a component bridge.</summary>
internal sealed class NativeCleanupClient : IAsyncDisposable
{
    private readonly ServiceProvider services;
    internal NativeCleanupBridge Bridge
    {
        get;
    }

    /// <summary>Supplies trusted fixed metadata and a native registration signing key, without any synthetic authentication result.</summary>
    internal NativeCleanupClient(NativeCleanupFixture issuer)
    {
        Bridge = new(issuer);
        var registrations = new ServiceCollection().AddLogging(logging => logging.ClearProviders());
        registrations.AddOpenIddict().AddClient(client =>
        {
            client.AllowAuthorizationCodeFlow().DisableTokenStorage().AddEphemeralSigningKey().AddEphemeralEncryptionKey();
            client.AddRegistration(new OpenIddictClientRegistration
            {
                ClientId = NativeCleanupProfile.Client,
                RegistrationId = NativeCleanupProfile.Client,
                Issuer = new Uri(NativeCleanupProfile.Issuer),
                RedirectUri = new Uri(NativeCleanupProfile.Callback),
                Configuration = new OpenIddictConfiguration
                {
                    Issuer = new Uri(NativeCleanupProfile.Issuer),
                    RevocationEndpoint = new Uri(new Uri(NativeCleanupProfile.Issuer), NativeCleanupProfile.Revoke),
                    RevocationEndpointAuthMethodsSupported = { OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt }
                },
                ClientAuthenticationMethods = { OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt },
                SigningCredentials = { new SigningCredentials(issuer.ClientSigning, SecurityAlgorithms.RsaSha256) }
            });
            client.UseSystemNetHttp(http => http.Configure(options => { options.HttpErrorPolicy = null; options.HttpResiliencePipeline = null; }));
        });
        registrations.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HandlerLifetime = Timeout.InfiniteTimeSpan;
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.AdditionalHandlers.Add(Bridge));
        });
        services = registrations.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Lets OpenIddict create its own child scope, sign a fresh client assertion, serialize the request and parse the response.</summary>
    internal async Task<OpenIddictResponse> RevokeAsync(string intent, string nonce, string hint = NativeCleanupProfile.SelectorType)
    {
        var result = await services.GetRequiredService<OpenIddictClientService>().RevokeTokenAsync(new()
        {
            RegistrationId = NativeCleanupProfile.Client,
            Token = intent,
            TokenTypeHint = hint,
            CancellationToken = TestContext.Current.CancellationToken,
            AdditionalRevocationRequestParameters = new Dictionary<string, OpenIddictParameter> { [NativeCleanupProfile.Nonce] = nonce }
        });
        return result.RevocationResponse;
    }

    /// <summary>Disposes native service and HTTP dependencies before the enclosing issuer releases its keys.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await services.DisposeAsync();
        }
        finally { Bridge.Dispose(); }
    }
}

/// <summary>Crosses an independent execution context with actual native request bytes; it proves no TLS or wire-loss behavior.</summary>
internal sealed class NativeCleanupBridge(NativeCleanupFixture issuer) : DelegatingHandler
{
    private const string EmptyJson = "{}";
    private const string JsonContentType = "application/json";
    internal int Sends
    {
        get; private set;
    }
    internal HashSet<string> ProofDigests { get; } = [];
    internal Exception? LastFailure
    {
        get; private set;
    }
    internal string? LastNativeError
    {
        get; private set;
    }
    internal bool EmptyResponse
    {
        get; set;
    }

    /// <summary>Prevents client ambient ownership from flowing into the independently scoped server component.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(() => DispatchAsync(request, cancellationToken), cancellationToken);
        }
    }

    /// <summary>Checks the exact destination and native proof-bearing shape, retaining only counts and irreversible test proof hashes.</summary>
    private async Task<HttpResponseMessage> DispatchAsync(HttpRequestMessage request, CancellationToken token)
    {
        Assert.Equal(new Uri(new Uri(NativeCleanupProfile.Issuer), NativeCleanupProfile.Revoke), request.RequestUri);
        Assert.Equal(HttpMethod.Post, request.Method);
        using var reader = new FormReader(await request.Content!.ReadAsStringAsync(token));
        var fields = (await reader.ReadFormAsync(token)).ToDictionary(pair => pair.Key, pair =>
        {
            Assert.Equal(1, pair.Value.Count);
            return pair.Value[0]!;
        }, StringComparer.Ordinal);
        Assert.Equal(OpenIddictConstants.ClientAssertionTypes.JwtBearer, fields[OpenIddictConstants.Parameters.ClientAssertionType]);
        Assert.False(fields.ContainsKey(OpenIddictConstants.Parameters.ClientSecret));
        var proof = fields[OpenIddictConstants.Parameters.ClientAssertion];
        ProofDigests.Add(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(proof))));
        Sends++;
        var result = await InvokeAsync(fields, token);
        return new HttpResponseMessage((HttpStatusCode)result.Status)
        {
            Content = new ByteArrayContent(EmptyResponse ? Encoding.UTF8.GetBytes(EmptyJson) : result.Body)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(JsonContentType) }
            }
        };
    }

    /// <summary>Retains exact server causation separately from the native client's deliberate error sanitization.</summary>
    private async Task<NativeCleanupResponse> InvokeAsync(Dictionary<string, string> fields, CancellationToken token)
    {
        LastFailure = null;
        LastNativeError = null;
        try
        {
            var result = await NativeCleanupHttp.InvokeAsync(issuer, NativeCleanupProfile.Revoke, fields, token);
            LastNativeError = result.NativeError;
            return result;
        }
        catch (Exception error)
        {
            LastFailure = error;
            throw;
        }
    }
}
