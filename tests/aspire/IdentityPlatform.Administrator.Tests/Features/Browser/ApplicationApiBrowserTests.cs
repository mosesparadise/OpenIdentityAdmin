using System.Diagnostics;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves real native resource validation and uncached current permission across independently hosted API roots.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationApiBrowserTests(BootstrapFixture fixture)
{
    private const int Success = 200;
    private const int Unauthorized = 401;
    private const int Forbidden = 403;
    private const int SignatureTail = 8;
    private const string Callback = "/callback";
    private const string Permission = "applications.read";
    private const string Invalid = "invalid";
    private const string Audience = "aud";
    private const string Issuer = "iss";
    private const string Scope = "scope";
    private const string Presenter = "client_id";
    private const string Expiry = "exp";
    private const string AccessType = "at+jwt";
    private const string IdentityType = "JWT";
    private const int PayloadPart = 1;
    private const int Seconds = 5;
    private const int RepeatedReads = 3;
    private static readonly string[] InvalidBindings = [Audience, Issuer, Scope, Presenter, Expiry];

    /// <summary>Actual issuer-signed variants with wrong resource, issuer, scope, presenter, lifetime and identity-token type cannot become API authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealAccessToken_When_NativeBindingsOrSignatureDiffer_Then_IndependentApiDeniesEverySubstitution(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
            await using var browser = await BrowserSession.CreateAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, await browser.Context.NewPageAsync());
            var api = environment.Api!.Address;
            await ApplicationAccessFixture.RequireAsync(api, access.Value, Success);
            await ApplicationAccessFixture.RequireAsync(api, null, Unauthorized);
            await ApplicationAccessFixture.RequireAsync(api, Invalid, Unauthorized);
            var credentials = environment.Issuer.Services.GetRequiredService<IssuerCredentials>();
            await ApplicationAccessFixture.RequireAsync(api, Sign(Payload(access.Value), credentials, AccessType), Success);
            foreach (var binding in InvalidBindings)
            {
                var claims = Payload(access.Value);
                Assert.True(claims.ContainsKey(binding));
                claims[binding] = binding == Expiry ? environment.Issuer.Account.Journey.Clock.Now.ToUnixTimeSeconds() : Invalid;
                await ApplicationAccessFixture.RequireAsync(api, Sign(claims, credentials, AccessType), Unauthorized);
            }
            await ApplicationAccessFixture.RequireAsync(api, Sign(Payload(access.Value), credentials, IdentityType), Unauthorized);
            var changedSignature = access.Value[..^SignatureTail] + Invalid;
            await ApplicationAccessFixture.RequireAsync(api, changedSignature, Unauthorized);
            await ApplicationAccessFixture.RequireAsync(api, access.Value, Success);
        });

    /// <summary>Removing the persisted read grant after warming two API roots denies both immediately and remains denied on subsequent reads.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_TwoWarmApis_When_ReadPermissionCommitsRemoval_Then_BothDenyWithinFiveSecondsWithoutClaimFallback(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
            await using var browser = await BrowserSession.CreateAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, await browser.Context.NewPageAsync());
            await using var second = await ApplicationApiHost.CreateAsync(environment.Issuer.Account.Journey, provider, environment.Profile.Issuer,
                new Uri(environment.Profile.ClientUri, Callback).AbsoluteUri);
            Uri[] nodes = [environment.Api!.Address, second.Address];
            foreach (var node in nodes)
            {
                await ApplicationAccessFixture.RequireAsync(node, access.Value, Success);
            }
            await using (var scope = environment.Issuer.Account.CreateScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
            }
            var committed = Stopwatch.GetTimestamp();
            foreach (var node in nodes)
            {
                await ApplicationAccessFixture.RequireAsync(node, access.Value, Forbidden);
            }
            Assert.True(Stopwatch.GetElapsedTime(committed) < TimeSpan.FromSeconds(Seconds));
            for (var index = 0; index < RepeatedReads; index++)
            {
                foreach (var node in nodes)
                {
                    await ApplicationAccessFixture.RequireAsync(node, access.Value, Forbidden);
                }
            }
        });

    /// <summary>Clones only an actual token's payload in memory to make isolated issuer-signed negative cases.</summary>
    private static Dictionary<string, object> Payload(string access) => JsonSerializer.Deserialize<Dictionary<string, object>>(
        Base64UrlEncoder.DecodeBytes(access.Split('.')[PayloadPart]))!;

    /// <summary>Uses the test issuer's real signing certificate while retaining the original stored token identity and authorization.</summary>
    private static string Sign(Dictionary<string, object> claims, IssuerCredentials credentials, string type) =>
        new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            TokenType = type,
            SigningCredentials = new SigningCredentials(new X509SecurityKey(credentials.Signing), SecurityAlgorithms.RsaSha256)
        });
}
