using IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Requires exact consent-only policy, original nonce and inert failure privacy independently of hostile HTTP metadata.</summary>
public sealed class ManagedFormPolicyTests
{
    private const string Client = "managed-policy-client";
    private const string Callback = "https://client.example.test:9443/callback?state=synthetic";
    private const string Origin = "https://client.example.test:9443";
    private const string Foreign = "https://foreign.example.test";
    private const string Referrer = "Referrer-Policy";
    private const string FormReferrer = "strict-origin";
    private const string InertReferrer = "no-referrer";
    private const string Nonce = "fixed-synthetic-nonce";
    private const string NonceItem = "CspNonce";
    private const string Policy = "default-src 'none'; script-src 'nonce-fixed-synthetic-nonce'; style-src 'self'; img-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'";
    private const string Separator = " ";
    private const string UnusedConnection = "Host=unused;Database=synthetic";
    private const string Failure = "synthetic-render-failure";

    /// <summary>Only consent permits its single selected origin; repeated rendering never retains an earlier exception in another phase.</summary>
    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, true)]
    [InlineData(4, false, false)]
    [InlineData(5, false, false)]
    [InlineData(6, false, false)]
    [InlineData(7, false, false)]
    public void Given_CurrentManagedPhase_When_FormPolicyIsRendered_Then_OnlyConsentPermitsItsBoundOrigin(int state, bool form, bool external)
    {
        var http = Context();
        var request = new ManagedHumanRequest { ClientId = Client, Callback = Callback };
        ManagedFormPolicy.Apply(http, request, Result(ManagedLoginState.PendingConsent));
        ManagedFormPolicy.Apply(http, request, Result((ManagedLoginState)state));
        Assert.True(http.Response.Headers.ContentSecurityPolicy == Policy + (external ? Separator + Origin : string.Empty));
        Assert.Equal(form ? FormReferrer : InertReferrer, http.Response.Headers[Referrer]);
        Assert.Equal(Nonce, http.Items[NonceItem]);
    }

    /// <summary>Even a caller error cannot turn wildcard, relative, non-TLS or credential-bearing destinations into a CSP source.</summary>
    [Theory]
    [InlineData("http://client.example.test/callback")]
    [InlineData("/callback")]
    [InlineData("https://user@client.example.test/callback")]
    [InlineData("https://client.example.test/callback#fragment")]
    [InlineData("https://*.example.test/callback")]
    public void Given_UnsafeCallback_When_ConsentPolicyIsRendered_Then_NoDestinationIsPublished(string callback)
    {
        var http = Context();
        var error = Record.Exception(() => ManagedFormPolicy.Apply(http,
            new ManagedHumanRequest { ClientId = Client, Callback = callback }, Result(ManagedLoginState.PendingConsent)));
        Assert.NotNull(error);
        Assert.Equal(Policy, http.Response.Headers.ContentSecurityPolicy);
        Assert.Equal(InertReferrer, http.Response.Headers[Referrer]);
    }

    /// <summary>A missing shared native nonce fails closed rather than inventing an independently nonced policy.</summary>
    [Fact]
    public void Given_MissingNativeNonce_When_ConsentPolicyIsRendered_Then_PresentationFailsClosed()
    {
        var http = Context();
        http.Items.Remove(NonceItem);
        Assert.Throws<InvalidOperationException>(() => ManagedFormPolicy.Apply(http,
            new ManagedHumanRequest { ClientId = Client, Callback = Callback }, Result(ManagedLoginState.PendingConsent)));
        Assert.Equal(Policy, http.Response.Headers.ContentSecurityPolicy);
    }

    /// <summary>A result for another client cannot be accidentally combined with this protected callback.</summary>
    [Fact]
    public void Given_ForeignValidatedClient_When_ConsentPolicyIsRendered_Then_NoDestinationIsPublished()
    {
        var http = Context();
        Assert.Throws<InvalidOperationException>(() => ManagedFormPolicy.Apply(http,
            new ManagedHumanRequest { ClientId = Foreign, Callback = Foreign }, Result(ManagedLoginState.PendingConsent)));
        Assert.Equal(Policy, http.Response.Headers.ContentSecurityPolicy);
        Assert.Equal(InertReferrer, http.Response.Headers[Referrer]);
    }

    /// <summary>The actual outer guard discards a staged consent exception on classified and unclassified renderer failures.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_StagedConsentPolicy_When_RenderingFails_Then_ActualGuardRestoresSameOriginAndInertHeaders(bool badRequest)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddIdentityPersistence(DatabaseProvider.PostgreSql, UnusedConnection);
        await using var container = services.BuildServiceProvider();
        await using var scope = container.CreateAsyncScope();
        var http = Context();
        http.RequestServices = scope.ServiceProvider;
        http.Request.Path = ManagedFormProof.Path;
        await using var body = new MemoryStream();
        http.Response.Body = body;
        await AccountRequestGuard.InvokeAsync(http, context =>
        {
            ManagedFormPolicy.Apply(context, new ManagedHumanRequest { ClientId = Client, Callback = Callback }, Result(ManagedLoginState.PendingConsent));
            throw badRequest ? new BadHttpRequestException(Failure) : new InvalidOperationException(Failure);
        }, _ => { });
        Assert.Equal(badRequest ? StatusCodes.Status400BadRequest : StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Equal(InertReferrer, http.Response.Headers[Referrer]);
        Assert.True(http.Response.Headers.ContentSecurityPolicy == Policy.Replace(Nonce, (string)http.Items[NonceItem]!, StringComparison.Ordinal));
    }

    /// <summary>Uses safe, fixed validated presentation metadata; this helper does not substitute for native provenance checks.</summary>
    private static ManagedHumanResult Result(ManagedLoginState state) => new(true, state, 1, Client, long.MaxValue);

    /// <summary>Supplies hostile transport metadata that must never determine the response destination.</summary>
    private static DefaultHttpContext Context()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = HostString.FromUriComponent(new Uri(Foreign));
        http.Request.Headers.Origin = Foreign;
        http.Request.QueryString = QueryString.Create(ManagedFormProof.Field, Foreign);
        http.Items[NonceItem] = Nonce;
        http.Response.Headers.ContentSecurityPolicy = Policy;
        http.Response.Headers[Referrer] = InertReferrer;
        return http;
    }
}
