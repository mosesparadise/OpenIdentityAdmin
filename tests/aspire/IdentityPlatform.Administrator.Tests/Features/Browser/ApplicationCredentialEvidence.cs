using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves the UI's original displayed credential authenticates natively before replacement, without obtaining a code, token or grant.</summary>
internal static class ApplicationCredentialEvidence
{
    private const string TokenPath = "/connect/token";
    private const string GrantType = "grant_type";
    private const string CodeGrant = "authorization_code";
    private const string Code = "code";
    private const string Callback = "redirect_uri";
    private const string Verifier = "code_verifier";
    private const string Basic = "Basic";
    private const string CredentialSeparator = ":";
    private const string Error = "error";
    private const string Description = "error_description";
    private const string ErrorUri = "error_uri";
    private const string InvalidClient = "invalid_client";
    private const string InvalidGrant = "invalid_grant";
    private const string SetCookie = "Set-Cookie";
    private const string Json = "application/json";
    private const int MaximumBytes = 4096;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(12);

    /// <summary>Varies only the secret between two requests sharing one original budget; invalid client proof and nonexistent code must remain distinguishable.</summary>
    internal static async Task RequireAsync(Uri issuer, string clientId, string secret, string callback)
    {
        ApplicationEditorBinding.Secret(secret);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(Budget);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
        var code = ManagedAuthorizationRequest.Random();
        var verifier = ManagedAuthorizationRequest.Random();
        await RequireErrorAsync(client, issuer, clientId, Wrong(secret), callback, code, verifier,
            HttpStatusCode.Unauthorized, InvalidClient, budget.Token);
        await RequireErrorAsync(client, issuer, clientId, secret, callback, code, verifier,
            HttpStatusCode.BadRequest, InvalidGrant, budget.Token);
    }

    /// <summary>Changes exactly one decoded credential byte, retaining canonical encoding and guaranteeing a distinct wrong proof.</summary>
    private static string Wrong(string secret)
    {
        var bytes = Convert.FromBase64String(secret);
        try
        {
            bytes[0] ^= 1;
            return Convert.ToBase64String(bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <summary>Sends one real Basic/code request and accepts only its exact bounded credential-free native error, without redirects or browser authority.</summary>
    private static async Task RequireErrorAsync(HttpClient client, Uri issuer, string clientId, string secret, string callback,
        string code, string verifier, HttpStatusCode status, string error, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(issuer, TokenPath))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [GrantType] = CodeGrant,
                [Code] = code,
                [Callback] = callback,
                [Verifier] = verifier
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(Basic, Convert.ToBase64String(Encoding.UTF8.GetBytes(
            Uri.EscapeDataString(clientId) + CredentialSeparator + Uri.EscapeDataString(secret))));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.True(response.Headers.CacheControl?.NoStore == true && response.Content.Headers.ContentType?.MediaType == Json);
        await response.Content.LoadIntoBufferAsync(MaximumBytes, token);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        var names = body.RootElement.EnumerateObject().Select(value => value.Name).ToArray();
        Assert.True(names.Length == names.Distinct(StringComparer.Ordinal).Count() && names.All(value => value is Error or Description or ErrorUri));
        Assert.True(body.RootElement.GetProperty(Error).GetString() == error);
    }
}
