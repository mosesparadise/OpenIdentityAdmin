using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Verifies explicit capability union, independent raw framing and monotonic post-cleanup publication expiry without database substitutes.</summary>
public sealed class ManagedProfileTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const int ManagedLifetimeSeconds = 300;
    private const int AdministratorLifetimeSeconds = 60;
    private const int OriginalDeadlineSeconds = 2;
    private const int LaterDeadlineSeconds = 5;
    private const string SecureScheme = "https";
    private const string InsecureScheme = "http";
    private const string Callback = "https://managed.example.test/callback";
    private const string Authorize = "/connect/authorize";
    private const string Query = "?client_id=managed-client&request_uri=urn:test";
    private const string Body = "unexpected";
    private const string Encoded = "/connect/%61uthorize";
    private const string Extra = "&resource=";
    private const string Duplicate = "&client_id=managed-client";
    private const string Alias = "&Client_Id=managed-client";
    private const long EpochSeconds = 1700000000;

    /// <summary>Managed enablement adds only shared-secret caching prerequisites, preserving administrator scope and disabling pilot scope.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_ExplicitManagedProfile_When_NativeOptionsComposed_Then_FixedAdministratorAndPilotCapabilitiesStaySeparate(bool managed)
    {
        var options = new OpenIddictServerOptions();
        ProtocolOptions.Configure(options, new(false, AdministratorRegistrationPolicy.Create(Callback, fixture.Key), managed));
        Assert.Equal(managed ? ["client_secret_basic", "private_key_jwt"] : ["private_key_jwt"], options.ClientAuthenticationMethods.Order());
        Assert.Equal(["identity.admin", "openid"], options.Scopes.Order());
        Assert.Equal(managed ? ["login", "none"] : ["none"], options.PromptValues.Order());
        Assert.Equal(TimeSpan.FromSeconds(managed ? ManagedLifetimeSeconds : AdministratorLifetimeSeconds), options.RequestTokenLifetime);
    }

    /// <summary>Fixed administrator/pilot identifiers and their case aliases never become managed candidates.</summary>
    [Theory]
    [InlineData("identity-admin-bff")]
    [InlineData("IDENTITY-ADMIN-BFF")]
    [InlineData("identityplatform-pilot")]
    [InlineData("IDENTITYPLATFORM-PILOT")]
    [InlineData("")]
    public void Given_FixedOrInvalidClient_When_ManagedCandidateChecked_Then_NoManagedAdmissionIsSelected(string client) => Assert.False(ManagedDemand.Candidate(client));

    /// <summary>Unframed body bytes, query ambiguity and path aliases reject before native extraction rather than relying on transport-declared length.</summary>
    [Theory]
    [InlineData(ManagedFraming.Valid)]
    [InlineData(ManagedFraming.Body)]
    [InlineData(ManagedFraming.RawPath)]
    [InlineData(ManagedFraming.Extra)]
    [InlineData(ManagedFraming.Duplicate)]
    [InlineData(ManagedFraming.Alias)]
    [InlineData(ManagedFraming.Insecure)]
    [InlineData(ManagedFraming.Post)]
    public async Task Given_ManagedAuthorizationTransport_When_RawFramingIsChecked_Then_OnlyExactBodylessGetIsAccepted(ManagedFraming kind)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = kind == ManagedFraming.Insecure ? InsecureScheme : SecureScheme;
        http.Request.Method = kind == ManagedFraming.Post ? HttpMethods.Post : HttpMethods.Get;
        http.Request.Path = Authorize;
        http.Request.QueryString = new(Query + (kind switch
        {
            ManagedFraming.Extra => Extra,
            ManagedFraming.Duplicate => Duplicate,
            ManagedFraming.Alias => Alias,
            _ => string.Empty
        }));
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = (kind == ManagedFraming.RawPath ? Encoded : Authorize) + http.Request.QueryString;
        if (kind == ManagedFraming.Body)
        {
            http.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Body));
        }
        var transaction = new OpenIddictServerTransaction { EndpointType = OpenIddictServerEndpointType.Authorization };
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        var context = new OpenIddictServerEvents.ProcessRequestContext(transaction);
        await ManagedTransport.ValidateAsync(context);
        Assert.Equal(kind != ManagedFraming.Valid, context.IsRejected);
        if (context.IsRejected)
        {
            Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, context.Error);
        }
    }

    /// <summary>Any later attempt to extend a retained expiry fails at the original equality boundary, including final response publication.</summary>
    [Fact]
    public void Given_RestrictedPublication_When_DeadlineIsExtendedAndOriginalExpires_Then_FinalAdmissionStillFails()
    {
        var clock = new PublicationClock(DateTimeOffset.FromUnixTimeSeconds(EpochSeconds));
        var request = new ProtocolStorageRequest(clock, CancellationToken.None);
        request.RestrictPublication(clock.Now.AddSeconds(OriginalDeadlineSeconds));
        request.RestrictPublication(clock.Now.AddSeconds(LaterDeadlineSeconds));
        request.RequirePublication(ProtocolResponseOutcome.ReadOnly);
        clock.Now = clock.Now.AddSeconds(OriginalDeadlineSeconds);
        Assert.Throws<InvalidOperationException>(() => request.RequirePublication(ProtocolResponseOutcome.ReadOnly));
    }

    /// <summary>Changes only authority wall time; elapsed request watchdog remains an independent real monotonic clock.</summary>
    private sealed class PublicationClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        /// <summary>Returns the explicit original publication boundary.</summary>
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

/// <summary>Names independent exact raw-input rejection scenarios.</summary>
public enum ManagedFraming
{
    Valid, Body, RawPath, Extra, Duplicate, Alias, Insecure, Post
}
