using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Exercises unowned transport capability misuse and the independent post-inference/profile fence.</summary>
public sealed class ProtocolHostRequestTests
{
    private const string PilotClient = "identityplatform-pilot";
    private const string AdministratorClient = "identity-admin-bff";

    /// <summary>Neither ordinary account access nor an unclassified request grants native credential-generation permission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_NonProtocolScope_When_ConfiguredRouteIsInferred_Then_NoFallbackCanReachExtraction(bool account)
    {
        var http = new DefaultHttpContext();
        var request = new ProtocolHostRequest();
        request.Bind(http, OpenIddictServerEndpointType.Unknown, account ? ProtocolHostMode.PilotAccount : ProtocolHostMode.Unknown);
        var outer = new OpenIddictServerEvents.ProcessRequestContext(Transaction(http));
        request.RequireUnowned(outer);
        request.RequireInferred(outer);
        outer.EndpointType = OpenIddictServerEndpointType.Authorization;
        Assert.Throws<InvalidOperationException>(() => request.RequireInferred(outer));
        outer.EndpointType = OpenIddictServerEndpointType.Unknown;
        Assert.Throws<InvalidOperationException>(() => request.RequireUnowned(new OpenIddictServerEvents.GenerateTokenContext(outer.Transaction)));
    }

    /// <summary>Actual extracted pilot identity is required before authentication and must remain on the original transaction.</summary>
    [Fact]
    public void Given_PilotTransport_When_ExtractionCompletes_Then_OnlyOriginalProfileCanContinue()
    {
        var http = new DefaultHttpContext();
        var request = new ProtocolHostRequest();
        request.Bind(http, OpenIddictServerEndpointType.Authorization, ProtocolHostMode.PilotProtocol);
        var outer = new OpenIddictServerEvents.ProcessRequestContext(Transaction(http));
        request.RequireUnowned(outer);
        outer.EndpointType = OpenIddictServerEndpointType.Authorization;
        request.RequireInferred(outer);
        request.RequireUnowned(new OpenIddictServerEvents.ExtractAuthorizationRequestContext(outer.Transaction));
        Assert.Throws<InvalidOperationException>(() => request.RequireUnowned(new OpenIddictServerEvents.ProcessAuthenticationContext(outer.Transaction)));
        outer.Transaction.Request = new OpenIddictRequest { ClientId = PilotClient };
        request.RequirePilotExtraction(outer);
        request.RequireUnowned(new OpenIddictServerEvents.ProcessAuthenticationContext(outer.Transaction));
        var foreign = Transaction(http);
        foreign.EndpointType = outer.EndpointType;
        foreign.Request = new OpenIddictRequest { ClientId = PilotClient };
        Assert.Throws<InvalidOperationException>(() => request.RequireUnowned(new OpenIddictServerEvents.ProcessAuthenticationContext(foreign)));
        outer.Transaction.Request.ClientId = AdministratorClient;
        Assert.Throws<InvalidOperationException>(() => request.RequireUnowned(new OpenIddictServerEvents.ProcessAuthenticationContext(outer.Transaction)));
    }

    /// <summary>Ownership and one-shot HTTP identity cannot be synthesized by a copied transaction or repeated outer request.</summary>
    [Fact]
    public void Given_BoundTransport_When_IdentityOrOwnershipIsSubstituted_Then_ContinuationIsRejected()
    {
        var http = new DefaultHttpContext();
        var request = new ProtocolHostRequest();
        request.Bind(http, OpenIddictServerEndpointType.Token, ProtocolHostMode.Owned);
        var outer = new OpenIddictServerEvents.ProcessRequestContext(Transaction(http)) { EndpointType = OpenIddictServerEndpointType.Token };
        Assert.Throws<InvalidOperationException>(() => request.RequireUnowned(outer));
        request.Track(outer);
        Assert.Throws<InvalidOperationException>(() => request.RequireInferred(outer));
        Assert.Throws<InvalidOperationException>(() => request.Track(outer));
        Assert.Throws<InvalidOperationException>(() => request.Bind(http, OpenIddictServerEndpointType.Token, ProtocolHostMode.PilotProtocol));
        var wrongHttp = new ProtocolHostRequest();
        wrongHttp.Bind(new DefaultHttpContext(), OpenIddictServerEndpointType.Unknown, ProtocolHostMode.Unknown);
        Assert.Throws<InvalidOperationException>(() => wrongHttp.RequireUnowned(outer));
    }

    /// <summary>Creates only native context identity; it supplies neither authenticated claims nor issuance permission.</summary>
    private static OpenIddictServerTransaction Transaction(HttpContext http)
    {
        var transaction = new OpenIddictServerTransaction();
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        return transaction;
    }
}
