using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks planned native row creation against the installed generator selection rather than duplicating its selection algorithm.</summary>
public sealed class AdministratorIssuancePlanTests
{
    private const string Client = "identity-admin-bff";
    private const string OtherClient = "other-client";
    private const string Authorization = "consumed-code-authorization";
    private const string RequestUri = "urn:ietf:params:oauth:request_uri:synthetic-reference";

    /// <summary>Actual native PAR, authorization and exchange flags map to their exact persisted creation categories.</summary>
    [Theory]
    [InlineData(OpenIddictServerEndpointType.PushedAuthorization)]
    [InlineData(OpenIddictServerEndpointType.Authorization)]
    [InlineData(OpenIddictServerEndpointType.Token)]
    public async Task Given_FixedNativeSignIn_When_GeneratorsAreEvaluated_Then_ExactCreationPlanIsSelected(OpenIddictServerEndpointType endpoint)
    {
        var context = await CreateAsync(endpoint);
        var plan = AdministratorIssuancePlan.Read(context);
        Assert.NotNull(plan);
        var expected = endpoint switch
        {
            OpenIddictServerEndpointType.PushedAuthorization => AdministratorTokenCreation.PushedRequest,
            OpenIddictServerEndpointType.Authorization => AdministratorTokenCreation.AuthorizationCode,
            _ => AdministratorTokenCreation.TokenExchange
        };
        Assert.Equal(expected, plan.Creation);
        var types = endpoint switch
        {
            OpenIddictServerEndpointType.PushedAuthorization => new[] { OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken },
            OpenIddictServerEndpointType.Authorization => [OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode],
            _ => [OpenIddictConstants.TokenTypeIdentifiers.AccessToken, OpenIddictConstants.TokenTypeIdentifiers.IdentityToken]
        };
        Assert.True(plan.TokenTypes.SetEquals(types));
    }

    /// <summary>Every unrelated credential-generation or inclusion flag denies the bounded plan before any native row creation.</summary>
    [Theory]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.GenerateDeviceCode))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.IncludeDeviceCode))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.GenerateUserCode))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.IncludeUserCode))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.GenerateRefreshToken))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.IncludeRefreshToken))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.GenerateIssuedToken))]
    [InlineData(nameof(OpenIddictServerEvents.ProcessSignInContext.IncludeIssuedToken))]
    public async Task Given_ExtraNativeOutput_When_PlanningCreation_Then_ReservationIsDenied(string flag)
    {
        var context = await CreateAsync(OpenIddictServerEndpointType.PushedAuthorization);
        typeof(OpenIddictServerEvents.ProcessSignInContext).GetProperty(flag)!.SetValue(context, true);
        Assert.Null(AdministratorIssuancePlan.Read(context));
    }

    /// <summary>A native authorization-cache request cannot consume the PAR category or issue an unbudgeted request token.</summary>
    [Fact]
    public async Task Given_AuthorizationCaching_When_NoParReferenceExists_Then_NoAdministratorPlanIsCreated()
    {
        var context = await CreateAsync(OpenIddictServerEndpointType.Authorization);
        context.Options.EnableAuthorizationRequestCaching = true;
        context.Request.RequestUri = null;
        await new OpenIddictServerHandlers.EvaluateGeneratedTokens().HandleAsync(context);
        Assert.True(context.GenerateRequestToken);
        Assert.Null(AdministratorIssuancePlan.Read(context));
    }

    /// <summary>Code issuance requires a new authorization while exchange must retain its existing code authorization.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_IncorrectAuthorizationReuse_When_PlanningCreation_Then_DeltaCannotBeMiscounted(bool exchange)
    {
        var context = await CreateAsync(exchange ? OpenIddictServerEndpointType.Token : OpenIddictServerEndpointType.Authorization);
        context.Principal!.SetAuthorizationId(exchange ? null : Authorization);
        Assert.Null(AdministratorIssuancePlan.Read(context));
    }

    /// <summary>Missing required output, switched client identity and terminal native contexts never receive a quota plan.</summary>
    [Theory]
    [InlineData(ContinuationChange.Generation)]
    [InlineData(ContinuationChange.Inclusion)]
    [InlineData(ContinuationChange.Client)]
    [InlineData(ContinuationChange.Rejected)]
    [InlineData(ContinuationChange.Handled)]
    [InlineData(ContinuationChange.Skipped)]
    public async Task Given_ChangedParContinuation_When_PlanningCreation_Then_NoPlanIsGranted(ContinuationChange change)
    {
        var context = await CreateAsync(OpenIddictServerEndpointType.PushedAuthorization);
        switch (change)
        {
            case ContinuationChange.Generation:
                context.GenerateRequestToken = false;
                break;
            case ContinuationChange.Inclusion:
                context.IncludeRequestToken = false;
                break;
            case ContinuationChange.Client:
                context.Request.ClientId = OtherClient;
                break;
            case ContinuationChange.Rejected:
                context.Reject();
                break;
            case ContinuationChange.Handled:
                context.HandleRequest();
                break;
            case ContinuationChange.Skipped:
                context.SkipRequest();
                break;
        }
        Assert.Null(AdministratorIssuancePlan.Read(context));
    }

    /// <summary>Names mutations independently of the production admission logic.</summary>
    public enum ContinuationChange
    {
        Generation, Inclusion, Client, Rejected, Handled, Skipped
    }

    /// <summary>Uses the installed evaluator with fixed synthetic request/principal inputs; no persistence or cryptographic acceptance is implied.</summary>
    private static async Task<OpenIddictServerEvents.ProcessSignInContext> CreateAsync(OpenIddictServerEndpointType endpoint)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Options = new OpenIddictServerOptions(),
            Logger = NullLogger.Instance,
            EndpointType = endpoint,
            Request = new OpenIddictRequest
            {
                ClientId = Client,
                ResponseType = OpenIddictConstants.ResponseTypes.Code,
                RequestUri = RequestUri,
                GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode
            },
            Response = new OpenIddictResponse()
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity()).SetScopes(OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope);
        if (endpoint == OpenIddictServerEndpointType.Token)
        {
            principal.SetAuthorizationId(Authorization);
        }
        var context = new OpenIddictServerEvents.ProcessSignInContext(transaction) { Principal = principal };
        await new OpenIddictServerHandlers.EvaluateGeneratedTokens().HandleAsync(context);
        return context;
    }
}
