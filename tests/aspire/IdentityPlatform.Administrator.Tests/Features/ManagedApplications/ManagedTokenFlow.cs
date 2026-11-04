using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Retains the real independent verifier/state/nonce and drives genuine protected managed human forms before native issuance.</summary>
internal sealed class ManagedTokenFlow(ManagedCacheTestContext test)
{
    private const string Grant = "grant_type";
    private const string CodeGrant = "authorization_code";
    private const string Redirect = "redirect_uri";
    private const string Code = "code";
    private const string Verifier = "code_verifier";
    private const string State = "state";
    private const string NonceField = "nonce";
    private const string Issuer = "iss";
    private const string ClientId = "client_id";
    private const string RequestUri = "request_uri";
    private const string Basic = "Basic";
    private const string CredentialSeparator = ":";
    private const int ResumeFields = 2;
    private readonly string verifier = NativeCacheProfile.Random();
    private Dictionary<string, string>? initial;
    internal Dictionary<string, string> Resume { get; private set; } = [];
    internal string RequestId { get; private set; } = string.Empty;
    internal string Nonce => initial![NonceField];

    /// <summary>Creates actual native caching and completes a fresh password/MFA/consent ceremony using native CSRF and replay state.</summary>
    internal async Task ApproveAsync()
    {
        await CacheAsync();
        await DecideAsync();
    }

    /// <summary>Creates a real native cached request for this API-created client's exact callback and independent PKCE/state/nonce.</summary>
    internal async Task CacheAsync()
    {
        initial = NativeCacheFlow.Initial(verifier, test.ClientIdentifier, test.CallbackUri);
        using var cached = await test.SendAsync(initial);
        Resume = await test.ResumeAsync(cached);
        await using var scope = test.Http.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var reference = Resume[RequestUri];
        var prefixed = reference.StartsWith(OpenIddictConstants.RequestUris.Prefixes.Generic, StringComparison.Ordinal);
        Assert.True(prefixed);
        var token = await manager.FindByReferenceIdAsync(reference[OpenIddictConstants.RequestUris.Prefixes.Generic.Length..], TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        RequestId = (await manager.GetIdAsync(token, TestContext.Current.CancellationToken))!;
    }

    /// <summary>Completes fresh native password/MFA and the explicit chosen consent decision for the retained original request.</summary>
    internal async Task DecideAsync(bool approve = true)
    {
        using var entry = await test.SendAsync(Resume);
        await ManagedHumanHttp.EntryAsync(entry);
        var form = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, entry.Headers.Location!);
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        using var factor = await ManagedHumanHttp.MfaAsync(test, mfa);
        await ManagedHumanHttp.EntryAsync(factor);
        var consent = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, factor.Headers.Location!);
        using var decision = await ManagedHumanHttp.DecideAsync(test, consent, approve);
        if (approve)
        {
            await ResumeRedirectAsync(test, decision, Resume);
        }
        else
        {
            await ManagedHumanHttp.EntryAsync(decision);
            await ManagedHumanHttp.TerminalAsync(test, decision.Headers.Location!);
        }
    }

    /// <summary>Requires the exact original issuer-local native request redirect with no body or new authority cookie.</summary>
    internal static async Task ResumeRedirectAsync(ManagedCacheTestContext test, HttpResponseMessage response, Dictionary<string, string>? expected = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.Location is { IsAbsoluteUri: false });
        var location = new Uri(test.Http.Environment.Profile.Issuer, response.Headers.Location!);
        Assert.True(location.GetLeftPart(UriPartial.Path) == new Uri(test.Http.Environment.Profile.Issuer, NativeCacheProfile.Authorize).AbsoluteUri && string.IsNullOrEmpty(location.Fragment));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.True(query.Count == ResumeFields && query[ClientId].Count == 1 && query[ClientId][0] == test.ClientIdentifier && query[RequestUri].Count == 1);
        if (expected is not null)
        {
            Assert.True(query[RequestUri][0] == expected[RequestUri]);
        }
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        ManagedHumanHttp.NoAuthorityCookie(response);
    }

    /// <summary>Obtains a genuine native code only after consent, checking exact RP origin/path, response fields, issuer and original state.</summary>
    internal async Task<string> CodeAsync()
    {
        using var response = await test.SendAsync(Resume);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.Location?.GetLeftPart(UriPartial.Path) == test.CallbackUri && string.IsNullOrEmpty(response.Headers.Location.Fragment));
        var fields = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.True(fields.Keys.ToHashSet(StringComparer.Ordinal).SetEquals([Code, State, Issuer]) && fields.All(pair => pair.Value.Count == 1));
        Assert.True(fields[State][0] == initial![State] && fields[Issuer][0] == test.Http.Environment.Profile.Issuer.AbsoluteUri && !string.IsNullOrEmpty(fields[Code][0]));
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        ManagedHumanHttp.NoAuthorityCookie(response);
        return fields[Code][0]!;
    }

    /// <summary>Uses actual Basic credentials and the independently retained original S256 verifier over real HTTPS.</summary>
    internal async Task<HttpResponseMessage> ExchangeAsync(string code, string? secret = null, string? proof = null, string? callback = null, HttpClient? client = null, string? clientId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NativeCacheProfile.Token)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Grant] = CodeGrant,
                [Redirect] = callback ?? test.CallbackUri,
                [Code] = code,
                [Verifier] = proof ?? verifier
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(Basic, Convert.ToBase64String(Encoding.UTF8.GetBytes(
            Uri.EscapeDataString(clientId ?? test.ClientIdentifier) + CredentialSeparator + Uri.EscapeDataString(secret ?? test.Secret))));
        return await (client ?? test.Http.IssuerClient).SendAsync(request, TestContext.Current.CancellationToken);
    }
}
