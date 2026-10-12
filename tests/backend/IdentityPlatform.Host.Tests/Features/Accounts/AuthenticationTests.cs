using System.Net;
using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Proves scheme separation and fail-closed behavior even for cryptographically valid synthetic tickets.</summary>
public sealed class AuthenticationTests
{
    private const string Subject = "synthetic-subject";
    private const string IdentityType = "synthetic-test";
    private const string CookieHeader = "Cookie";
    private const string CookieSeparator = "=";
    private const string SetCookieHeader = "Set-Cookie";
    private const string PilotScheme = "Identity.Application";

    /// <summary>Rejects validly protected administrator and cross-scheme tickets without creating a session.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ProtectedTickets_When_AdministratorAuthenticationRuns_Then_NoPrincipalIsAccepted(bool pilot)
    {
        await using var host = await AccountHost.StartAsync(pilot, true, app => app.MapGet(AccountHost.Probe, (Delegate)AuthenticateAsync));
        var options = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var administrator = options.Get(AccountSchemes.Administrator);
        Assert.Equal("__Host-identity-administrator", administrator.Cookie.Name);
        Assert.True(administrator.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, administrator.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, administrator.Cookie.SameSite);
        Assert.False(administrator.SlidingExpiration);
        Assert.Null(administrator.ForwardDefault);
        await RequireDeniedAsync(host.Client, administrator, AccountSchemes.Administrator, administrator.Cookie.Name!);
        if (pilot)
        {
            var pilotOptions = options.Get(PilotScheme);
            await RequireDeniedAsync(host.Client, pilotOptions, PilotScheme, administrator.Cookie.Name!);
            await RequireDeniedAsync(host.Client, pilotOptions, PilotScheme, pilotOptions.Cookie.Name!);
        }
    }

    /// <summary>Returns success only if the explicit administrator scheme incorrectly accepted a ticket.</summary>
    private static async Task<IResult> AuthenticateAsync(HttpContext context) =>
        (await context.AuthenticateAsync(AccountSchemes.Administrator)).Succeeded ? Results.Ok() : Results.Unauthorized();

    /// <summary>Protects through the actual native data format, then checks HTTP denial without cookie issuance or redirects.</summary>
    private static async Task RequireDeniedAsync(HttpClient client, CookieAuthenticationOptions options, string scheme, string cookieName)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Subject)], IdentityType));
        var properties = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1)
        };
        var ticket = options.TicketDataFormat.Protect(new AuthenticationTicket(principal, properties, scheme));
        using var request = new HttpRequestMessage(HttpMethod.Get, AccountHost.Probe);
        request.Headers.Add(CookieHeader, cookieName + CookieSeparator + ticket);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookieHeader));
    }

    /// <summary>Blocks arbitrary issuance without the actual committed journey result, even when a handler supplies an authenticated principal.</summary>
    [Fact]
    public async Task Given_AdministratorComposition_When_SignInIsRequested_Then_NoCookieIsIssued()
    {
        await using var host = await AccountHost.StartAsync(false, true, app => app.MapGet(AccountHost.Probe, SignInAsync));
        using var response = await host.Client.GetAsync(AccountHost.Probe, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(response.Headers.Contains(SetCookieHeader));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    /// <summary>Invokes the actual authentication service with a synthetic authenticated principal.</summary>
    private static Task SignInAsync(HttpContext context) => context.SignInAsync(AccountSchemes.Administrator,
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Subject)], IdentityType)));

    /// <summary>Challenges remain local status responses and cannot redirect into pilot login.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AdministratorComposition_When_ChallengedOrForbidden_Then_NoLoginRedirectOccurs(bool forbidden)
    {
        await using var host = await AccountHost.StartAsync(false, true, app => app.MapGet(AccountHost.Probe,
            (HttpContext context) => forbidden ? context.ForbidAsync(AccountSchemes.Administrator) : context.ChallengeAsync(AccountSchemes.Administrator)));
        using var response = await host.Client.GetAsync(AccountHost.Probe, TestContext.Current.CancellationToken);
        Assert.Equal(forbidden ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }
}
