using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Preserves configured policy authority and inert-result privacy independently of request metadata and prior form rendering.</summary>
public sealed class BrowserFormPolicyTests
{
    private const string UnusedConnection = "Host=unused;Database=synthetic";
    private const string SourceSeparator = " ";
    private const string FormPolicy = "strict-origin";
    private const string InertPolicy = "no-referrer";
    private const string LogoutHint = "logout_hint";
    private const string ReturnPath = "bff/logout/return";
    private const string ReturnParameter = "return";
    private const string Issuer = "https://issuer.example.test/";
    private const string Client = "https://administrator.example.test:8443/";
    private const string ClientOrigin = "https://administrator.example.test:8443";
    private const string Referrer = "Referrer-Policy";
    private const string Nonce = "fixed-synthetic-nonce";
    private const string NonceItem = "CspNonce";
    private const string Foreign = "https://foreign.example.test";
    private const string Placeholder = "non-authenticating-test-placeholder";
    private const string AccountPolicy = "default-src 'none'; script-src 'nonce-fixed-synthetic-nonce'; style-src 'self'; img-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'";
    private const string LogoutPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    private const string PresentationPolicy = "; style-src 'sha256-nIUXs2C1YiiVplk3G8QC7SH3kEerGX19ls/gblno1Fo='; script-src 'sha256-w+uuhquW+ZQV55tUvccOSS+TKS9Hbuz6bhtay55K5ho='";

    /// <summary>Host, Origin and query cannot replace the configured nondefault-port BFF origin or alter the existing script nonce/directives.</summary>
    [Fact]
    public void Given_HostileRequestMetadata_When_MfaFormPolicyIsBuilt_Then_OnlyConfiguredOriginAndOriginalNonceAreUsed()
    {
        var http = Hostile();
        http.Items[NonceItem] = Nonce;
        AccountRequestGuard.AllowBrowserFormReturn(http, new(Issuer, Client));
        Assert.True(http.Response.Headers.ContentSecurityPolicy == AccountPolicy + SourceSeparator + ClientOrigin);
        Assert.Equal(Nonce, http.Items[NonceItem]);
    }

    /// <summary>A missing native page nonce cannot silently generate a permissive or independently nonced form policy.</summary>
    [Fact]
    public void Given_MissingNativeNonce_When_MfaFormPolicyIsBuilt_Then_RenderingFailsClosed()
    {
        var http = Hostile();
        Assert.Throws<InvalidOperationException>(() => AccountRequestGuard.AllowBrowserFormReturn(http, new(Issuer, Client)));
        Assert.Equal(0, http.Response.Headers.ContentSecurityPolicy.Count);
    }

    /// <summary>Actual local result rendering overwrites the form's exception and retains every existing CSP directive.</summary>
    [Fact]
    public void Given_LogoutForm_When_LocalResultIsRendered_Then_OriginOnlyPolicyDoesNotLeakIntoResult()
    {
        var http = Hostile();
        _ = BrowserLogoutPage.Form(http, Placeholder);
        Assert.Equal(FormPolicy, http.Response.Headers[Referrer]);
        Assert.Equal(LogoutPolicy + PresentationPolicy, http.Response.Headers.ContentSecurityPolicy);
        _ = BrowserLogoutPage.Committed(http);
        Assert.Equal(InertPolicy, http.Response.Headers[Referrer]);
        Assert.Equal(LogoutPolicy + PresentationPolicy, http.Response.Headers.ContentSecurityPolicy);
    }

    /// <summary>The renderer uses its separately supplied configured return, while a subsequent inert denial restores the default policy.</summary>
    [Fact]
    public async Task Given_UntrustedNativeFields_When_ConfirmationPolicyIsRendered_Then_OnlyConfiguredReturnControlsCsp()
    {
        var http = Hostile();
        await using var output = new MemoryStream();
        http.Response.Body = output;
        var request = new OpenIddictRequest { ClientId = Placeholder, PostLogoutRedirectUri = Foreign, State = Placeholder };
        request.SetParameter(LogoutHint, Placeholder);
        await IssuerLogoutPage.ConfirmAsync(http, request, Placeholder, Placeholder, new Uri(Client + ReturnPath));
        Assert.Equal(FormPolicy, http.Response.Headers[Referrer]);
        Assert.True(http.Response.Headers.ContentSecurityPolicy == LogoutPolicy + SourceSeparator + ClientOrigin + PresentationPolicy);
        await IssuerLogoutPage.IncompleteAsync(http);
        Assert.Equal(InertPolicy, http.Response.Headers[Referrer]);
        Assert.Equal(LogoutPolicy + PresentationPolicy, http.Response.Headers.ContentSecurityPolicy);
    }

    /// <summary>Actual safe failure handling must remove a staged form exception before publishing inert text, without opening a database connection.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_StagedFormPolicy_When_RendererFails_Then_ActualGuardRestoresInertResponsePolicy(bool badRequest)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddIdentityPersistence(DatabaseProvider.PostgreSql, UnusedConnection);
        await using var container = services.BuildServiceProvider();
        await using var scope = container.CreateAsyncScope();
        var http = Hostile();
        http.RequestServices = scope.ServiceProvider;
        http.Request.Path = BrowserAccountProfile.MfaPath;
        await using var output = new MemoryStream();
        http.Response.Body = output;
        var entered = false;
        var incidents = 0;
        await AccountRequestGuard.InvokeAsync(http, context =>
        {
            entered = true;
            context.Response.Headers[Referrer] = FormPolicy;
            AccountRequestGuard.AllowBrowserFormReturn(context, new(Issuer, Client));
            throw badRequest ? new BadHttpRequestException(Placeholder) : new InvalidOperationException(Placeholder);
        }, _ => incidents++);
        Assert.True(entered);
        Assert.Equal(badRequest ? 0 : 1, incidents);
        Assert.Equal(badRequest ? StatusCodes.Status400BadRequest : StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Equal(InertPolicy, http.Response.Headers[Referrer]);
        Assert.True(http.Response.Headers.ContentSecurityPolicy == AccountPolicy.Replace(Nonce, (string)http.Items[NonceItem]!, StringComparison.Ordinal));
    }

    /// <summary>Supplies hostile metadata only; these pure rendering controls do not fabricate authenticated native validation.</summary>
    private static DefaultHttpContext Hostile()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = HostString.FromUriComponent(new Uri(Foreign));
        http.Request.Headers.Origin = Foreign;
        http.Request.QueryString = QueryString.Create(ReturnParameter, Foreign);
        return http;
    }
}
