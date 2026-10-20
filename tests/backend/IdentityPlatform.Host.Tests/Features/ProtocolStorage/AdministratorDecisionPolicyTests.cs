using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Separates fixed issued audience policy from native RFC 8707 request-resource validation and forbids projection widening.</summary>
public sealed class AdministratorDecisionPolicyTests
{
    private const string Scopes = "openid identity.admin";
    private const string ExtraScope = "extra";
    private const string Reference = "urn:ietf:params:oauth:request_uri:synthetic-reference";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
    private const string ForeignResource = "https://other.example.test/resource";
    private const string Subject = "synthetic-subject";
    private const string Session = "synthetic-session";
    private const string Token = "synthetic-token";
    private const string Authorization = "synthetic-authorization";

    /// <summary>Each fixed endpoint rejects every caller resource override while the omitted indicator permits the fixed scope mapping.</summary>
    [Theory]
    [InlineData(OpenIddictServerEndpointType.PushedAuthorization, null)]
    [InlineData(OpenIddictServerEndpointType.PushedAuthorization, "")]
    [InlineData(OpenIddictServerEndpointType.PushedAuthorization, AdministratorProtocolIdentity.Resource)]
    [InlineData(OpenIddictServerEndpointType.PushedAuthorization, ForeignResource)]
    [InlineData(OpenIddictServerEndpointType.Authorization, null)]
    [InlineData(OpenIddictServerEndpointType.Authorization, "")]
    [InlineData(OpenIddictServerEndpointType.Authorization, AdministratorProtocolIdentity.Resource)]
    [InlineData(OpenIddictServerEndpointType.Authorization, ForeignResource)]
    [InlineData(OpenIddictServerEndpointType.Token, null)]
    [InlineData(OpenIddictServerEndpointType.Token, "")]
    [InlineData(OpenIddictServerEndpointType.Token, AdministratorProtocolIdentity.Resource)]
    [InlineData(OpenIddictServerEndpointType.Token, ForeignResource)]
    public async Task Given_FixedProfile_When_CallerSelectsResource_Then_OnlyServerScopeMappingCanDetermineAudience(OpenIddictServerEndpointType endpoint, string? resource)
    {
        var request = Request();
        if (resource is not null)
        {
            request.SetParameter(OpenIddictConstants.Parameters.Resource, resource);
        }
        var http = new DefaultHttpContext();
        if (resource is not null)
        {
            http.Request.QueryString = QueryString.Create(OpenIddictConstants.Parameters.Resource, resource);
        }
        var transaction = new OpenIddictServerTransaction { Request = request, EndpointType = endpoint };
        transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        Assert.Equal(resource is not null, (await ValidateAsync(transaction)).IsRejected);
    }

    /// <summary>Invokes each actual fixed-profile validation adapter on its native event type.</summary>
    private static async Task<OpenIddictServerEvents.BaseValidatingContext> ValidateAsync(OpenIddictServerTransaction transaction)
    {
        var policy = new AdministratorRequestPolicy();
        OpenIddictServerEvents.BaseValidatingContext context;
        switch (transaction.EndpointType)
        {
            case OpenIddictServerEndpointType.PushedAuthorization:
                var par = new OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext(transaction);
                await policy.HandleAsync(par);
                context = par;
                break;
            case OpenIddictServerEndpointType.Authorization:
                var authorize = new OpenIddictServerEvents.ValidateAuthorizationRequestContext(transaction);
                await policy.HandleAsync(authorize);
                context = authorize;
                break;
            default:
                var exchange = new OpenIddictServerEvents.ValidateTokenRequestContext(transaction);
                await policy.HandleAsync(exchange);
                context = exchange;
                break;
        }
        return context;
    }

    /// <summary>Original protected code scopes, resource, presenter and native identifiers must match before projection can add fixed claims.</summary>
    [Theory]
    [InlineData(Change.None)]
    [InlineData(Change.NarrowScope)]
    [InlineData(Change.ExtraScope)]
    [InlineData(Change.Resource)]
    [InlineData(Change.Presenter)]
    [InlineData(Change.Authorization)]
    [InlineData(Change.Type)]
    public void Given_ProtectedCode_When_OriginalBindingDiffers_Then_ProjectionCannotWidenIt(Change change)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        principal.SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        principal.SetTokenId(Token).SetAuthorizationId(Authorization);
        principal.SetClaim(OpenIddictConstants.Claims.Subject, Subject).SetClaim(AdministratorPrincipal.SessionId, Session);
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope);
        principal.SetResources(AdministratorProtocolIdentity.Resource).SetPresenters(AdministratorProtocolIdentity.ClientId);
        Assert.True(AdministratorDecisionPolicy.IsCode(principal));
        switch (change)
        {
            case Change.NarrowScope:
                principal.SetScopes(OpenIddictConstants.Scopes.OpenId);
                break;
            case Change.ExtraScope:
                principal.SetScopes(OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope, ExtraScope);
                break;
            case Change.Resource:
                principal.SetResources(ForeignResource);
                break;
            case Change.Presenter:
                principal.SetPresenters(Subject);
                break;
            case Change.Authorization:
                principal.SetAuthorizationId(null);
                break;
            case Change.Type:
                principal.SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken);
                break;
        }
        Assert.Equal(change == Change.None, AdministratorDecisionPolicy.IsCode(principal));
    }

    /// <summary>Builds only public fixed-profile request metadata; no signature or admission authority is fabricated.</summary>
    private static OpenIddictRequest Request() => new()
    {
        ClientId = AdministratorProtocolIdentity.ClientId,
        ResponseType = OpenIddictConstants.ResponseTypes.Code,
        GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
        RequestUri = Reference,
        Prompt = OpenIddictConstants.PromptValues.None,
        Scope = Scopes,
        CodeChallenge = Challenge,
        CodeChallengeMethod = OpenIddictConstants.CodeChallengeMethods.Sha256
    };

    public enum Change
    {
        None, NarrowScope, ExtraScope, Resource, Presenter, Authorization, Type
    }
}
