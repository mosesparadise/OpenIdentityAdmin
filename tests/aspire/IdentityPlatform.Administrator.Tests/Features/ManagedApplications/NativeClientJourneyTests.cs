using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises a public native registration through the real issuer, fresh human consent, S256 exchange and token verification.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientJourneyTests(BootstrapFixture fixture)
{
    private const string Client = "native-application-link";
    private const string Callback = "https://mobile.example.test/callback";
    private const string NativeProfile = "managed-native-v1";
    private static readonly string[] Redirects = [Callback];
    private const string Display = "Native application link";
    private const string IncorrectSecret = "unused-native-client-secret";

    /// <summary>Requires a strong native code verifier and validates the resulting user-bound tokens without a client secret.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PublicNativeApplication_When_S256AndConsentComplete_Then_SecretlessExchangeReturnsBoundTokens(string provider)
    {
        await using var http = await ApplicationHttpFixture.CreateAsync(fixture, provider, managed: true);
        var registration = await RegisterAsync(http);
        var flow = new ManagedTokenFlow(registration);
        await flow.ApproveAsync();
        var code = await flow.CodeAsync();
        using var wrong = await flow.ExchangeAsync(code, secret: IncorrectSecret);
        Assert.True(wrong.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized);
        await ManagedJourneyEvidence.ExchangeAsync(registration, flow, code, denyManagement: true);
    }

    /// <summary>Registers through the protected BFF and binds the returned native client identity to the exact submitted settings.</summary>
    private static async Task<ManagedCacheTestContext> RegisterAsync(ApplicationHttpFixture http)
    {
        var body = JsonSerializer.Serialize(new
        {
            clientId = Client,
            displayName = Display,
            redirectUris = Redirects
        });
        using var response = await http.CreateAsync(body, native: true);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<ApplicationDetails>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(details);
        Assert.Equal(NativeProfile, details.Profile);
        Assert.Equal(Client, details.ClientId);
        Assert.Equal(Callback, Assert.Single(details.RedirectUris));
        Assert.Null(details.Credential);
        return new(http, details.Id, Client, Callback, null);
    }
}
