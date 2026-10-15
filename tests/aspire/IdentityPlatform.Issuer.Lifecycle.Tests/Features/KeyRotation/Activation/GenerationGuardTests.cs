using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol.Keys;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.KeyRotation.Activation;

[Collection("distribution")]
public sealed class GenerationGuardTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Real_certificate_tail_expiry_and_request_override_fail_with_native_errors_and_fresh_recovery(string provider)
    {
        using var shortMaterial = new IssuerMaterialFixture();
        await using var fixture = await KeyRotationEnvironment.CreateAsync(provider);
        var host = fixture.Host;
        var cookies = new CookieContainer();
        using var first = host.NodeClient("auth", cookies, autoRedirect: true);
        var pending = await TestLogin.BeginAsync(host, first);
        var continuation = await pending.CompleteAsync(host, first);
        shortMaterial.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, notAfter: DateTimeOffset.UtcNow.AddSeconds(80));
        using var imported = IssuerCredentials.Load(shortMaterial.Configuration, DateTimeOffset.UtcNow);
        var expires = new DateTimeOffset(imported.Signing.NotAfter.ToUniversalTime());
        await fixture.UseControlledSigningAsync("auth-secondary", shortMaterial.Settings);
        await WaitUntilAsync(expires.AddSeconds(-50));
        Assert.True(DateTimeOffset.UtcNow < expires.AddSeconds(-10), "Certificate expired before its positive-validity tail guard control.");
        var beforeCode = await ExchangeSnapshot.CaptureAsync(host);
        using (var second = host.NodeClient("auth-secondary", cookies))
        using (var denied = await second.GetAsync(continuation, TestContext.Current.CancellationToken))
        {
            RequireCallbackError(host, denied, pending.Flow, Errors.ServerError, ActiveKeyPolicy.SafeDescription);
        }
        Assert.True(beforeCode.TokenIds.SetEquals((await ExchangeSnapshot.CaptureAsync(host)).TokenIds), "Rejected code generation added native token rows.");
        await WaitUntilAsync(expires.AddMilliseconds(500));
        Assert.True(DateTimeOffset.UtcNow > expires);
        using var issuer = host.NodeClient("auth", autoRedirect: true);
        var code = await ExchangeEvidence.IssueAsync(host, issuer);
        var beforeTokens = await ExchangeSnapshot.CaptureAsync(host);
        using (var target = host.NodeClient("auth-secondary"))
        using (var denied = await host.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, transport: target,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, denied.StatusCode);
            Assert.True(denied.Headers.Location is null, "Failed token generation redirected.");
            Assert.Equal("application/json", denied.Content.Headers.ContentType?.MediaType);
            var text = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            SafeAssertions.ContainsNoSensitiveMaterial(text, code.Flow.Code, code.Flow.Verifier, host.Password, host.ClientSecret);
            using var body = JsonDocument.Parse(text);
            Assert.True(body.RootElement.GetProperty("error").GetString() == Errors.ServerError &&
                body.RootElement.GetProperty("error_description").GetString() == ActiveKeyPolicy.SafeDescription);
            Assert.True(body.RootElement.EnumerateObject().All(property => property.Name is "error" or "error_description" or "error_uri"), "Guard returned unexpected fields.");
        }
        Assert.True(beforeTokens.TokenIds.SetEquals((await ExchangeSnapshot.CaptureAsync(host)).TokenIds), "Access generation guard ran after token entry creation.");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var entry = await manager.FindByReferenceIdAsync(code.Flow.Code, TestContext.Current.CancellationToken);
            Assert.NotNull(entry);
            Assert.True(await manager.HasStatusAsync(entry, Statuses.Redeemed, TestContext.Current.CancellationToken), "Native redemption side effect was not observed.");
        }
        await fixture.ChangeAsync("auth-secondary", KeyStage.OldOnly);
        using (var recovery = host.NodeClient("auth-secondary", autoRedirect: true))
        {
            var fresh = await host.AuthorizeAsync(recovery);
            using var accepted = await DistributionEvidence.RedeemAsync(host, "auth-secondary", fresh);
        }
        await RequestOverrideAsync(fixture);
        await LateIdentityFailureAsync(host);
    }

    private static async Task LateIdentityFailureAsync(IssuerEnvironment host)
    {
        using var browser = host.NodeClient("auth", autoRedirect: true);
        var code = await ExchangeEvidence.IssueAsync(host, browser);
        var before = await ExchangeSnapshot.CaptureAsync(host);
        string generatedTokenId;
        // Native-pipeline integration only: the normal pilot rejects client lifetime overrides.
        // An explicitly supplied native principal lifetime reaches the later ID generation boundary.
        using (var runtime = host.RuntimeServices())
        await using (var scope = runtime.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IOpenIddictServerDispatcher>();
            var transaction = new OpenIddictServerTransaction
            {
                Options = runtime.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value,
                Logger = NullLogger.Instance,
                CancellationToken = TestContext.Current.CancellationToken,
                BaseUri = host.Issuer,
                RequestUri = new Uri(host.Issuer, "/connect/token"),
                EndpointType = OpenIddictServerEndpointType.Token,
                Request = new OpenIddictRequest
                {
                    ClientId = "identityplatform-pilot",
                    GrantType = GrantTypes.AuthorizationCode,
                    Code = code.Flow.Code,
                    CodeVerifier = code.Flow.Verifier,
                    RedirectUri = host.RelyingParty.Callback
                },
                Response = new OpenIddictResponse()
            };
            var validated = new ValidateTokenContext(transaction)
            {
                Token = code.Flow.Code,
                ValidTokenTypes = { TokenTypeIdentifiers.Private.AuthorizationCode },
                DisableAudienceValidation = true
            };
            await dispatcher.DispatchAsync(validated);
            Assert.False(validated.IsRejected);
            Assert.NotNull(validated.Principal);
            var authenticated = new ProcessAuthenticationContext(transaction) { AuthorizationCodePrincipal = validated.Principal };
            transaction.SetProperty(typeof(ProcessAuthenticationContext).FullName!, authenticated);
            var signIn = new ProcessSignInContext(transaction)
            {
                Principal = validated.Principal.Clone(_ => true).SetIdentityTokenLifetime(TimeSpan.FromSeconds(301))
            };
            await dispatcher.DispatchAsync(signIn);
            Assert.True(signIn.IsRejected && signIn.Error == Errors.ServerError && signIn.ErrorDescription == ActiveKeyPolicy.SafeDescription);
            Assert.False(string.IsNullOrEmpty(signIn.AccessToken), "The test did not reach failure after native access generation.");
            var generated = new JsonWebToken(signIn.AccessToken);
            Assert.True(generated.TryGetClaim(Claims.Private.TokenId, out var tokenId), "Generated access token has no native identity.");
            generatedTokenId = tokenId.Value;
            Assert.True(string.IsNullOrEmpty(signIn.IdentityToken) && string.IsNullOrEmpty(transaction.Response.AccessToken) &&
                string.IsNullOrEmpty(transaction.Response.IdToken), "Rejected native pipeline returned credentials.");
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var entry = await manager.FindByReferenceIdAsync(code.Flow.Code, TestContext.Current.CancellationToken);
            Assert.NotNull(entry);
            Assert.True(await manager.HasStatusAsync(entry, Statuses.Redeemed, TestContext.Current.CancellationToken));
            var after = await ExchangeSnapshot.CaptureAsync(host);
            var added = after.TokenIds.Except(before.TokenIds, StringComparer.Ordinal).ToArray();
            Assert.True(before.TokenIds.IsSubsetOf(after.TokenIds), "Late rejection removed an existing native token row.");
            Assert.True(added.Length == 1 && added[0] == generatedTokenId, "Late ID rejection must leave exactly the generated access entry.");
            var access = await manager.FindByIdAsync(added[0], TestContext.Current.CancellationToken);
            Assert.NotNull(access);
            Assert.True(await manager.GetTypeAsync(access, TestContext.Current.CancellationToken) == TokenTypeIdentifiers.AccessToken &&
                await manager.HasStatusAsync(access, Statuses.Valid, TestContext.Current.CancellationToken));
            Assert.True(await manager.GetSubjectAsync(access, TestContext.Current.CancellationToken) == await manager.GetSubjectAsync(entry, TestContext.Current.CancellationToken) &&
                await manager.GetApplicationIdAsync(access, TestContext.Current.CancellationToken) == await manager.GetApplicationIdAsync(entry, TestContext.Current.CancellationToken) &&
                await manager.GetAuthorizationIdAsync(access, TestContext.Current.CancellationToken) == await manager.GetAuthorizationIdAsync(entry, TestContext.Current.CancellationToken),
                "Earlier access entry changed the original code's identity binding.");
        }
        using var recovery = host.NodeClient("auth", autoRedirect: true);
        var fresh = await host.AuthorizeAsync(recovery);
        using var tokens = await DistributionEvidence.RedeemAsync(host, "auth", fresh);
    }

    private static async Task RequestOverrideAsync(KeyRotationEnvironment fixture)
    {
        var host = fixture.Host;
        await SettingsAsync(host, 301);
        try
        {
            var before = await ExchangeSnapshot.CaptureAsync(host);
            var flow = host.RelyingParty.Prepare();
            using var transport = host.NodeClient("auth");
            using var start = await transport.GetAsync(host.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
            using var denied = await transport.GetAsync(start.Headers.Location!, TestContext.Current.CancellationToken);
            RequireCallbackError(host, denied, flow, Errors.InvalidRequest, null);
            Assert.True(before.TokenIds.SetEquals((await ExchangeSnapshot.CaptureAsync(host)).TokenIds), "Rejected request generation persisted a token.");
        }
        finally { await SettingsAsync(host, null); }
        await SettingsAsync(host, 300);
        CachedRequestEvidence request;
        try
        {
            request = await CachedRequestEvidence.CreateAsync(host, "auth");
            await KeyEvidence.RequirePayloadAsync(fixture, request.Reference, TokenTypeIdentifiers.Private.RequestToken,
                fixture.Original.Signing.Thumbprint, fixture.Original.Encryption.Thumbprint);
        }
        finally { await SettingsAsync(host, null); }
        await request.CompleteAsync(host, "auth");
    }

    private static async Task SettingsAsync(IssuerEnvironment host, int? seconds)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync("identityplatform-pilot", TestContext.Current.CancellationToken);
        Assert.NotNull(application);
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, TestContext.Current.CancellationToken);
        descriptor.Settings.Clear();
        if (seconds is { } value)
        {
            descriptor.Settings.Add(Settings.TokenLifetimes.RequestToken, TimeSpan.FromSeconds(value).ToString("c", System.Globalization.CultureInfo.InvariantCulture));
        }
        await manager.UpdateAsync(application, descriptor, TestContext.Current.CancellationToken);
    }

    private static void RequireCallbackError(IssuerEnvironment host, HttpResponseMessage response, TestFlow flow, string error, string? description)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var target = response.Headers.Location;
        Assert.True(target is not null && target.IsAbsoluteUri && target.Fragment.Length == 0 && target.GetLeftPart(UriPartial.Path) == host.RelyingParty.Callback, "Guard used an unregistered callback.");
        var values = QueryHelpers.ParseQuery(target.Query);
        Assert.True(values["iss"] == host.Issuer.AbsoluteUri, "Guard callback changed the canonical issuer.");
        Assert.True(values["error"] == error && values["state"] == flow.State, "Unexpected native guard error/state.");
        if (description is not null)
        {
            Assert.True(values["error_description"] == description, "Guard description was not sanitized.");
        }
        Assert.True(values.Keys.All(key => key is "error" or "error_description" or "error_uri" or "state" or "iss"), "Guard returned an unexpected credential field.");
    }

    private static async Task WaitUntilAsync(DateTimeOffset instant)
    {
        var delay = instant - DateTimeOffset.UtcNow;
        Assert.True(delay < TimeSpan.FromSeconds(81));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, TestContext.Current.CancellationToken);
        }
    }
}
