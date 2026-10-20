using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using ClientSetup = IdentityPlatform.Admin.Bff.Features.Authentication.Protocol.Setup;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Connects the actual native client and genuine account issuer across independent instances and persistent Sessions.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeClientInteroperabilityTests(BootstrapFixture fixture)
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Client = "https://administrator.example.test/";
    private const string Callback = "https://administrator.example.test/callback";
    private const string KeyId = "native-interoperability-assertion";
    private const int SecondStateCount = 2;
    private const int ThirdStateCount = 3;
    private const string SecureAttribute = "secure";
    private const string HttpOnlyAttribute = "httponly";
    private const string SameSiteAttribute = "samesite=lax";
    private const string CredentialSection = "ClientCredentials";

    /// <summary>Real PAR, S256, private-key proof and state/correlation succeed once; replay, absent cookie and issuer error consume state without another exchange.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurableNativeClient_When_SeparateInstanceProcessesCallback_Then_OnlyOneBoundExchangeCompletes(string provider)
    {
        using var material = new NativeClientCredentialFixture();
        using var firstKeys = Load(material);
        using var secondKeys = Load(material);
        var profile = Profile(firstKeys);
        await using var issuer = await AdministratorProtocolScenario.CreateAsync(fixture, provider, profile.Registration);
        Assert.Equal(profile.Issuer, issuer.Issuer);
        await using var database = await fixture.LeaseSessionsAsync(provider);
        await using var first = new NativeClientHttp(database, issuer, profile, firstKeys);
        await using var second = new NativeClientHttp(database, issuer, profile, secondKeys);
        var challenge = await ChallengeAsync(first);
        await RequireStatesAsync(second, total: 1, redeemed: 0);
        var callback = await AuthorizeAsync(issuer, challenge, cookie: true);
        var accepted = await second.InvokeAsync(callback, challenge.Cookie);
        RequireClean(accepted);
        RequireAccepted(accepted);
        Assert.Equal(0, accepted.BodyLength);
        Assert.True(accepted.Authentication?.Succeeded == true);
        Assert.True(accepted.Authentication.Principal!.GetClaim(OpenIddictConstants.Claims.Subject) == issuer.Account.ActorId);
        foreach (var value in accepted.Cookies)
        {
            RequireCookie(value, attributes: false);
        }
        await RequireStatesAsync(first, total: 1, redeemed: 1);
        var replay = await first.InvokeAsync(callback, challenge.Cookie);
        NativeClientDenial.RequireReplay(replay);
        await RequireStatesAsync(second, total: 1, redeemed: 1);
        await DeniedAsync(first, second, issuer, issuerCookie: true, expectedStates: SecondStateCount);
        await DeniedAsync(first, second, issuer, issuerCookie: false, expectedStates: ThirdStateCount);
        Assert.Equal(3, first.Bridge.ParSends + second.Bridge.ParSends);
        Assert.Equal(1, first.Bridge.TokenSends + second.Bridge.TokenSends);
    }

    /// <summary>Distinguishes a valid callback missing correlation from a genuine issuer denial carrying valid correlation.</summary>
    private static async Task DeniedAsync(NativeClientHttp first, NativeClientHttp second, AdministratorProtocolScenario issuer,
        bool issuerCookie, int expectedStates)
    {
        var challenge = await ChallengeAsync(first);
        var callback = await AuthorizeAsync(issuer, challenge, issuerCookie);
        var denied = await second.InvokeAsync(callback, issuerCookie ? null : challenge.Cookie);
        RequireClean(denied);
        Assert.Equal(StatusCodes.Status400BadRequest, denied.Status);
        Assert.False(denied.Authentication?.Succeeded == true);
        await RequireStatesAsync(first, expectedStates, expectedStates);
    }

    /// <summary>Requires an actual native redirect and a distinct secure native correlation cookie, without exposing either value.</summary>
    internal static async Task<NativeClientHttpResult> ChallengeAsync(NativeClientHttp client)
    {
        var result = await client.InvokeAsync();
        RequireClean(result);
        Assert.Equal(StatusCodes.Status302Found, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Location));
        var cookieCount = result.Cookies.Length;
        Assert.Equal(1, cookieCount);
        RequireCookie(result.Cookies[0], attributes: true);
        return result;
    }

    /// <summary>Reports only booleans when a native cookie violates its policy, never the correlation value.</summary>
    private static void RequireCookie(string cookie, bool attributes)
    {
        var prefix = cookie.StartsWith(ClientSetup.CorrelationPrefix, StringComparison.Ordinal);
        Assert.True(prefix);
        if (attributes)
        {
            var secure = cookie.Contains(SecureAttribute, StringComparison.OrdinalIgnoreCase);
            var httpOnly = cookie.Contains(HttpOnlyAttribute, StringComparison.OrdinalIgnoreCase);
            var sameSite = cookie.Contains(SameSiteAttribute, StringComparison.OrdinalIgnoreCase);
            Assert.True(secure);
            Assert.True(httpOnly);
            Assert.True(sameSite);
        }
    }

    /// <summary>Follows only the exact native issuer destination with the real account cookie, preserving returned state/code bytes.</summary>
    internal static async Task<Uri> AuthorizeAsync(AdministratorProtocolScenario issuer, NativeClientHttpResult challenge, bool cookie)
    {
        var location = new Uri(challenge.Location);
        Assert.True(location.GetLeftPart(UriPartial.Path) == new Uri(issuer.Issuer, AdministratorProtocolScenario.AuthorizePath).AbsoluteUri);
        var fields = QueryHelpers.ParseQuery(location.Query).ToDictionary(pair => pair.Key, pair =>
        {
            Assert.Equal(1, pair.Value.Count);
            return pair.Value[0]!;
        });
        var result = await NativeAccountHttp.InvokeAsync(issuer, AdministratorProtocolScenario.AuthorizePath, fields, cookie);
        NativeAccountFlow.RequireSuccess(result, StatusCodes.Status302Found);
        var callback = new Uri(result.Location);
        Assert.True(callback.GetLeftPart(UriPartial.Path) == Callback);
        var response = QueryHelpers.ParseQuery(callback.Query);
        Assert.True(response.TryGetValue(OpenIddictConstants.Parameters.State, out var state) && state.Count == 1 && !string.IsNullOrEmpty(state[0]));
        if (cookie)
        {
            Assert.False(response.ContainsKey(OpenIddictConstants.Parameters.Error));
            Assert.True(response.TryGetValue(OpenIddictConstants.Parameters.Code, out var code) && code.Count == 1 && !string.IsNullOrEmpty(code[0]));
        }
        else
        {
            Assert.True(response.TryGetValue(OpenIddictConstants.Parameters.Error, out var error) && error.Count == 1 && error[0] == OpenIddictConstants.Errors.LoginRequired);
            Assert.False(response.ContainsKey(OpenIddictConstants.Parameters.Code));
        }
        return callback;
    }

    /// <summary>Reads actual committed statuses through a fresh native scope; no state payload or reference value is printed.</summary>
    internal static Task<bool> RequireStatesAsync(NativeClientHttp client, int total, int redeemed) =>
        new ProtocolStorageRequest(client.Services.GetRequiredService<TimeProvider>(), TestContext.Current.CancellationToken)
            .RunAsync(client.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                var statuses = await scope.Owner.RunAsync(current => new ValueTask<List<string?>>(scope.Owner.Database
                    .Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Select(row => row.Status).ToListAsync(current)), token);
                Assert.Equal(total, statuses.Count);
                Assert.Equal(redeemed, statuses.Count(value => value == OpenIddictConstants.Statuses.Redeemed));
                Assert.Equal(total - redeemed, statuses.Count(value => value == OpenIddictConstants.Statuses.Valid));
                return true;
            });

    /// <summary>Imports independently owned durable client certificates from protected synthetic files.</summary>
    internal static NativeClientCredentials Load(NativeClientCredentialFixture material) => NativeClientCredentials.Load(
        new ConfigurationBuilder().AddInMemoryCollection(material.Settings()).Build().GetSection(CredentialSection), DateTimeOffset.UtcNow);

    /// <summary>Shares the exact immutable native client profile between positive and awaited-mutation provider controls.</summary>
    internal static NativeClientProfile Profile(NativeClientCredentials credentials, Uri? issuer = null) =>
        NativeClientProfile.Create(issuer?.AbsoluteUri ?? Issuer, Client, Callback, KeyId, credentials);

    private static readonly System.Text.CompositeFormat BaselineStatus = System.Text.CompositeFormat.Parse("Status={0}; native={1}");

    /// <summary>Identifies a failed positive baseline using only status and a closed allowlist of native error categories.</summary>
    internal static void RequireAccepted(NativeClientHttpResult result)
    {
        RequireClean(result);
        Assert.True(result.Status == StatusCodes.Status200OK, string.Format(System.Globalization.CultureInfo.InvariantCulture, BaselineStatus, result.Status, result.Rejection));
        Assert.True(result.Authentication?.Succeeded == true);
    }

    /// <summary>Reports only safe exception type/stack, never native error descriptions or credential-bearing responses.</summary>
    internal static void RequireClean(NativeClientHttpResult result) =>
        Assert.True(result.Failure is null, result.Failure?.GetType().FullName + Environment.NewLine + result.Failure?.StackTrace);
}
