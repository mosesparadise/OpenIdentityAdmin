using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

internal static class RestartEvidence
{
    public static async Task<TestFlow> SilentAsync(IssuerEnvironment environment, CookieContainer cookies, TestFlow original, bool allowed)
    {
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var flow = environment.RelyingParty.Prepare(exchange: allowed, prompt: "none");
        flow.ExpectedSessionId = original.ExpectedSessionId;
        flow.ExpectedAuthenticatedAt = original.ExpectedAuthenticatedAt;
        var uri = environment.RelyingParty.StartUri(flow);
        for (var hop = 0; hop < 8; hop++)
        {
            Assert.True(uri.GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority) ||
                uri.GetLeftPart(UriPartial.Authority) == environment.RelyingParty.BaseUri.GetLeftPart(UriPartial.Authority), "Unexpected redirect origin.");
            Assert.True(uri.AbsolutePath is "/start" or "/connect/authorize" or "/callback", "Silent continuity navigated to an interactive page.");
            using var response = await browser.GetAsync(uri, TestContext.Current.CancellationToken);
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out var headers) &&
                headers.Any(value => value.StartsWith(PilotProfile.SessionCookie + "=", StringComparison.Ordinal) && !value.StartsWith(PilotProfile.SessionCookie + "=;", StringComparison.Ordinal)),
                "Silent continuity replaced the application ticket.");
            if (response.Headers.Location is { } location)
            {
                Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.SeeOther, "Unexpected redirect status.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            Assert.True(uri.AbsolutePath == "/callback" && response.StatusCode == HttpStatusCode.OK, "Silent flow did not reach its bound callback.");
            var result = await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.True(allowed ? result && flow.Validated && flow.Error.Length == 0 : !result && flow.Error == "login_required" && flow.Code.Length == 0,
                "Silent flow did not preserve the expected eligibility and identity.");
            return flow;
        }
        throw new InvalidOperationException("Silent flow exceeded its redirect bound.");
    }

    public static async Task<string> SessionsAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LocalIdentitySession>()
            .AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));
    }

    public static async Task<string> StateAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var state = new
        {
            Sessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken),
            Interactions = await database.Set<LoginInteraction>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken),
            Tokens = await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken),
            Audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }

    public static async Task<LoginInteraction> InteractionAsync(IssuerEnvironment environment, string id)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>().AsNoTracking()
            .SingleAsync(value => value.Id == id, TestContext.Current.CancellationToken);
    }

    public static async Task RequireUnchangedAsync(IssuerEnvironment environment, string before) =>
        Assert.True(before == await StateAsync(environment), "Denied request changed persisted interaction, session, token or audit state.");
}
