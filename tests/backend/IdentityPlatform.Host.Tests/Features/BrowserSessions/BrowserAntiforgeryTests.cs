using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Exercises the native protected antiforgery token with independent browser/generation snapshots; cookie rotation alone is insufficient.</summary>
public sealed class BrowserAntiforgeryTests
{
    private const string CookieSeparator = "; ";
    private const string TokenHeader = "X-CSRF";
    private const int LifetimeSeconds = 300;
    private const int NextGeneration = 2;

    /// <summary>Native signature, cookie and additional data must all validate; a retained old antiforgery cookie cannot authorize a new generation.</summary>
    [Theory]
    [InlineData(BrowserCsrfFault.None, true)]
    [InlineData(BrowserCsrfFault.OtherBrowser, false)]
    [InlineData(BrowserCsrfFault.NextGeneration, false)]
    [InlineData(BrowserCsrfFault.MissingSnapshot, false)]
    [InlineData(BrowserCsrfFault.MissingCookie, false)]
    [InlineData(BrowserCsrfFault.MalformedToken, false)]
    public async Task Given_NativeAntiforgeryToken_When_RequestBindingChanges_Then_OnlyExactOriginalBrowserGenerationPasses(BrowserCsrfFault fault, bool expected)
    {
        await using var services = Services();
        var browser = SessionHandle.Create().Digest;
        var issuance = Context(services, Binding(browser, 1));
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        var binding = fault == BrowserCsrfFault.MissingSnapshot ? null :
            Binding(fault == BrowserCsrfFault.OtherBrowser ? SessionHandle.Create().Digest : browser,
                fault == BrowserCsrfFault.NextGeneration ? NextGeneration : 1);
        var request = Context(services, binding);
        request.Request.Method = HttpMethods.Post;
        request.Request.Headers[TokenHeader] = fault == BrowserCsrfFault.MalformedToken ? SessionHandle.Create().Handle : tokens.RequestToken;
        if (fault != BrowserCsrfFault.MissingCookie)
        {
            request.Request.Headers.Cookie = string.Join(CookieSeparator, issuance.Response.Headers.SetCookie.Select(value => value!.Split(';')[0]));
        }
        Assert.Equal(expected, await antiforgery.IsRequestValidAsync(request));
    }

    /// <summary>No durable read snapshot means no token issuance; missing later contexts and expired generations never become bootstrap generation zero.</summary>
    [Fact]
    public void Given_MissingOrExpiredDurableRead_When_RequestBindingIsPrepared_Then_NoLaterGenerationFallsBackToInitial()
    {
        var browser = SessionHandle.Create().Digest;
        var now = BffSessionFixture.Start;
        Assert.NotNull(BrowserRequestBinding.FromRead(browser, 0, null, now));
        Assert.Null(BrowserRequestBinding.FromRead(browser, 1, null, now));
        Assert.Null(BrowserRequestBinding.FromRead(browser, -1, null, now));
        Assert.Null(BrowserRequestBinding.FromRead(browser, 0, Row(browser, 1), now));
        Assert.Null(BrowserRequestBinding.FromRead(browser, 1, Row(browser, 1), now.AddSeconds(LifetimeSeconds)));
        Assert.Null(BrowserRequestBinding.FromRead(SessionHandle.Create().Digest, 1, Row(browser, 1), now));
        Assert.Throws<InvalidOperationException>(() => new BrowserAntiforgeryData().GetAdditionalData(new DefaultHttpContext()));
    }

    /// <summary>Registers the real ASP.NET Core mechanism with ephemeral test protection, without creating product routes or reusing an issuer provider.</summary>
    private static ServiceProvider Services()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAntiforgery(options => { options.HeaderName = TokenHeader; options.Cookie.SecurePolicy = CookieSecurePolicy.Always; });
        services.AddSingleton<IAntiforgeryAdditionalDataProvider, BrowserAntiforgeryData>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Attaches only a server-created immutable feature, independent of request cookies or submitted fields.</summary>
    private static DefaultHttpContext Context(IServiceProvider services, BrowserRequestBinding? binding)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Features.Set(binding);
        return context;
    }

    /// <summary>Builds a valid saved-generation observation for native mechanism controls, not provider persistence evidence.</summary>
    private static BrowserRequestBinding Binding(string digest, long generation) =>
        BrowserRequestBinding.FromRead(digest, generation, Row(digest, generation), BffSessionFixture.Start)!;

    /// <summary>Uses independent fixed time and positive revision/correlation to isolate generation additional-data behavior.</summary>
    private static BrowserContext Row(string digest, long generation) => new()
    {
        Digest = digest,
        Generation = generation,
        Revision = 1,
        CorrelationId = Guid.NewGuid(),
        ObservedAt = BffSessionFixture.Start.UtcTicks,
        ExpiresAt = BffSessionFixture.Start.AddSeconds(LifetimeSeconds).UtcTicks
    };
}

/// <summary>Enumerates independent native cookie/token and durable snapshot failures.</summary>
public enum BrowserCsrfFault
{
    None, OtherBrowser, NextGeneration, MissingSnapshot, MissingCookie, MalformedToken
}
