using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Supplies synthetic issuer responses through the real native HTTP pipeline, retaining native parsing, state and cryptographic validation.</summary>
internal sealed class SampleNativeIssuer : IDisposable
{
    private const string HttpsText = "https";
    private const string WellKnownOpenidConfigurationText = "/.well-known/openid-configuration";
    private const string ConnectJwksText = "/connect/jwks";
    private const string ConnectTokenText = "/connect/token";
    private const string ConnectUserinfoText = "/connect/userinfo";
    private const string UnexpectedSyntheticIssuerRouteText = "Unexpected synthetic issuer route.";
    private const string CompressedEncoding = "gzip";
    private const string AuthorizationEndpoint = "connect/authorize";
    private const string TokenEndpoint = "connect/token";
    private const string UntrustedKeys = "untrusted-keys";
    private const string KeyEndpoint = "connect/jwks";
    private const string UserInfoEndpoint = "connect/userinfo";
    private const string PushedAuthorizationEndpoint = "connect/par";
    private const string CodeParameter = "code";
    private const string QueryText = "query";
    private const string AuthorizationCodeText = "authorization_code";
    private const string OpenidText = "openid";
    private const string OrdersReadText = "orders.read";
    private const string SampleEmailText = "sample.email";
    private const string S256Text = "S256";
    private const string ClientSecretBasicText = "client_secret_basic";
    private const string RS256Text = "RS256";
    private const string PublicText = "public";
    private const string RSAText = "RSA";
    private const string SampleTestText = "sample-test";
    private const string SigText = "sig";
    private const string BasicPrefix = "Basic ";
    private const string SampleTestSyntheticSecretText = "sample-test:synthetic-secret";
    private const string GrantParameter = "grant_type";
    private const string SyntheticCodeText = "synthetic-code";
    private const string CallbackText = "callback";
    private const string RedirectParameter = "redirect_uri";
    private const string VerifierParameter = "code_verifier";
    private const string JWTText = "JWT";
    private const int IdentityTokenMinutes = 2;
    private const string SubjectClaim = "sub";
    private const string SyntheticSubjectText = "synthetic-subject";
    private const string NonceParameter = "nonce";
    private const string WrongText = "wrong";
    private const string SyntheticServerOnlyAccessTokenText = "synthetic-server-only-access-token";
    private const string BearerScheme = "Bearer";
    private const int AccessTokenSeconds = 120;
    private const string OpenidOrdersReadSampleEmailText = "openid orders.read sample.email";
    private const string SyntheticExampleInvalidText = "synthetic@example.invalid";
    private const string JsonMediaType = "application/json";

    internal const string Issuer = "https://issuer.dev.localhost:17443/";
    internal const string Client = "https://sample.dev.localhost:17446/";
    private readonly RSA rsa = RSA.Create(2048);
    internal string? Nonce
    {
        get; set;
    }
    internal string? Challenge
    {
        get; set;
    }
    internal string? HostileResponse
    {
        get; set;
    }
    internal string? Fault
    {
        get; set;
    }
    internal Func<Task>? BeforeProfile
    {
        get; set;
    }
    internal System.Collections.Concurrent.ConcurrentDictionary<string, int> Calls { get; } = new(StringComparer.Ordinal);
    internal int TokenCalls
    {
        get; private set;
    }
    internal int UserInfoCalls
    {
        get; private set;
    }

    /// <summary>Creates a fresh delegating interception without replacing the required native primary HttpClientHandler.</summary>
    internal DelegatingHandler Handler() => new Interception(this);

    /// <summary>Accepts only fixed issuer destinations and dispatches finite protocol fixtures with independently specified fields.</summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Assert.Equal(new Uri(Issuer).Authority, request.RequestUri!.Authority);
        Assert.Equal(HttpsText, request.RequestUri.Scheme);
        var path = request.RequestUri.AbsolutePath;
        Calls.AddOrUpdate(path, 1, (_, count) => count + 1);
        if (path == ConnectUserinfoText && BeforeProfile is not null)
        {
            await BeforeProfile();
        }
        var response = path switch
        {
            WellKnownOpenidConfigurationText => Json(Discovery()),
            ConnectJwksText => Json(Keys()),
            ConnectTokenText => await TokensAsync(request, token),
            ConnectUserinfoText => Profile(),
            _ => throw new InvalidOperationException(UnexpectedSyntheticIssuerRouteText)
        };
        if (path == HostileResponse)
        {
            response.Content.Headers.ContentEncoding.Add(CompressedEncoding);
        }
        return response;
    }

    /// <summary>Publishes only the real fixed endpoint/capability profile; no sample-specific parsing shortcut is installed.</summary>
    private object Discovery() => new
    {
        issuer = Issuer,
        authorization_endpoint = Issuer + AuthorizationEndpoint,
        token_endpoint = Issuer + TokenEndpoint,
        jwks_uri = Issuer + (Fault == KeyEndpoint ? UntrustedKeys : KeyEndpoint),
        userinfo_endpoint = Issuer + UserInfoEndpoint,
        pushed_authorization_request_endpoint = Issuer + PushedAuthorizationEndpoint,
        response_types_supported = new[] { CodeParameter },
        response_modes_supported = new[] { QueryText },
        grant_types_supported = new[] { AuthorizationCodeText },
        scopes_supported = new[] { OpenidText, OrdersReadText, SampleEmailText },
        code_challenge_methods_supported = new[] { S256Text },
        token_endpoint_auth_methods_supported = new[] { ClientSecretBasicText },
        id_token_signing_alg_values_supported = new[] { RS256Text },
        authorization_response_iss_parameter_supported = true,
        subject_types_supported = new[] { PublicText }
    };

    /// <summary>Returns only the synthetic RSA public key used to verify the real native identity-token response.</summary>
    private object Keys()
    {
        var key = rsa.ExportParameters(false);
        return new
        {
            keys = new[] { new { kty = RSAText, kid = SampleTestText, use = SigText, alg = RS256Text, n = WebEncoders.Base64UrlEncode(key.Modulus!), e = WebEncoders.Base64UrlEncode(key.Exponent!) } }
        };
    }

    /// <summary>Checks actual Basic/S256 code redemption and signs a standards-shaped ID token with the original native nonce.</summary>
    private async Task<HttpResponseMessage> TokensAsync(HttpRequestMessage request, CancellationToken token)
    {
        TokenCalls++;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(BasicPrefix + Convert.ToBase64String(Encoding.ASCII.GetBytes(SampleTestSyntheticSecretText)), request.Headers.Authorization?.ToString());
        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(token));
        Assert.Equal(AuthorizationCodeText, form[GrantParameter].ToString());
        Assert.Equal(SyntheticCodeText, form[CodeParameter].ToString());
        Assert.Equal(Client + CallbackText, form[RedirectParameter].ToString());
        Assert.Equal(Challenge, WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(form[VerifierParameter].ToString()))));
        var key = new RsaSecurityKey(rsa) { KeyId = SampleTestText };
        var identity = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = SampleTestText,
            TokenType = JWTText,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(IdentityTokenMinutes),
            Claims = new Dictionary<string, object> { [SubjectClaim] = SyntheticSubjectText, [NonceParameter] = Fault == NonceParameter ? WrongText : Nonce! },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });
        return Json(new
        {
            access_token = SyntheticServerOnlyAccessTokenText,
            token_type = BearerScheme,
            expires_in = AccessTokenSeconds,
            id_token = identity,
            scope = OpenidOrdersReadSampleEmailText
        });
    }

    /// <summary>Counts unexpected automatic UserInfo traffic separately from explicitly requested profile activity.</summary>
    private HttpResponseMessage Profile()
    {
        UserInfoCalls++;
        return Json(new
        {
            sub = SyntheticSubjectText,
            email = SyntheticExampleInvalidText,
            email_verified = false
        });
    }

    /// <summary>Serializes bounded native protocol responses as JSON over an intercepted HTTPS request.</summary>
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, JsonMediaType) };

    /// <summary>Signs a synthetic API token with the same public key exposed through the native discovery/JWKS responses.</summary>
    internal string ApiToken()
    {
        const string audience = "orders-api";
        const string type = "at+jwt";
        const string scope = "scope";
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            TokenType = type,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(IdentityTokenMinutes),
            Claims = new Dictionary<string, object> { [SubjectClaim] = SyntheticSubjectText, [scope] = OrdersReadText },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = SampleTestText }, SecurityAlgorithms.RsaSha256)
        });
    }

    /// <summary>Releases the ephemeral private test signing key after the native host has stopped.</summary>
    public void Dispose() => rsa.Dispose();

    /// <summary>Intercepts actual emitted native HTTP without modifying its primary transport configuration or authentication pipeline.</summary>
    private sealed class Interception(SampleNativeIssuer issuer) : DelegatingHandler
    {
        /// <summary>Passes cancellation into the finite synthetic issuer; no real network calls occur.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => issuer.SendAsync(request, cancellationToken);
    }
}
