using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Runs native sign-in in a separate response child and withholds all success output until issuance commit and actual cleanup.</summary>
internal sealed class AdministratorIssuanceCoordinator(IServiceScopeFactory factory, AdministratorRegistrationPolicy policy)
{
    private static readonly object PermitKey = new();

    /// <summary>Retains original native contexts while resolving every native handler from the fresh child provider.</summary>
    internal async ValueTask RunAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        var request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        var passthrough = context.EndpointType == OpenIddictServerEndpointType.PushedAuthorization ? null : AdministratorPassthrough.Claim(context);
        var origin = NativeErrorOrigin.Find(context.Transaction);
        origin?.BeginIssuance(context);
        var result = await ProtocolResponseChild.RunAsync(http, factory, async (scope, response, token) =>
        {
            await using var issuance = new AdministratorIssuance(scope, context, policy);
            context.Transaction.SetProperty(typeof(AdministratorIssuance).FullName!, issuance);
            try
            {
                var native = scope.Services.GetRequiredService<OpenIddictServerDispatcher>();
                await ProtocolStorageDispatch.RunAsync(scope.Owner, () => context.CancellationToken,
                    current => context.CancellationToken = current, () => native.DispatchAsync(context));
                if (context.IsRejected)
                {
                    await issuance.CompleteDenialAsync(token);
                    return false;
                }
                await response.SealAsync();
                if (!await issuance.CommitAsync(token))
                {
                    throw ProtocolStorageOwner.Unavailable();
                }
                return true;
            }
            finally
            {
                context.Transaction.SetProperty<AdministratorIssuance>(typeof(AdministratorIssuance).FullName!, null);
                context.Transaction.SetProperty<NativeIssuanceProof>(typeof(NativeIssuanceProof).FullName!, null);
            }
        });
        origin?.EndIssuance(context);
        if (result.Result && result.Output is { } output)
        {
            await output.CopyAndSealParentAsync(context.CancellationToken);
            http.Items.Add(PermitKey, new Publication(request, context, passthrough, output));
        }
    }

    /// <summary>Claims the private success permit once; the ordinary HTTP boundary still requires every write phase to have committed.</summary>
    internal static void ClaimPublication(HttpContext http, OpenIddictServerEvents.ProcessRequestContext context)
    {
        if (!http.Items.Remove(PermitKey, out var value) || value is not Publication permit ||
            !context.IsRequestHandled || context.IsRejected || context.IsRequestSkipped ||
            permit.Passthrough is not null || permit.SignIn.EndpointType != OpenIddictServerEndpointType.PushedAuthorization ||
            !ReferenceEquals(ProtocolStorageAmbient.Current?.Request, permit.Request) || !ReferenceEquals(context.Transaction, permit.SignIn.Transaction))
        {
            ProtocolStorageAmbient.Current?.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        RequirePublication(permit);
    }

    /// <summary>Claims endpoint success only through the actual native passthrough and completed sign-in on the original request.</summary>
    internal static void ClaimEndpointPublication(HttpContext http)
    {
        if (!http.Items.Remove(PermitKey, out var value) || value is not Publication permit ||
            !ReferenceEquals(ProtocolStorageAmbient.Current?.Request, permit.Request) ||
            !ReferenceEquals(permit.SignIn.Transaction.GetHttpRequest()?.HttpContext, http) ||
            permit.Passthrough is null || !permit.Passthrough.Allows(permit.SignIn))
        {
            ProtocolStorageAmbient.Current?.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        RequirePublication(permit);
    }

    /// <summary>Rechecks terminal native success and the unchanged all-write-phases-committed publication gate.</summary>
    private static void RequirePublication(Publication permit)
    {
        if (!permit.SignIn.IsRequestHandled || permit.SignIn.IsRequestSkipped || permit.SignIn.IsRejected)
        {
            permit.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        permit.Output.RequireFrozenParent();
        permit.Request.RequirePublication(ProtocolResponseOutcome.Committed);
    }

    /// <summary>Retains only original identities and frozen output after actual child disposal; it contains no scoped service.</summary>
    private sealed record Publication(ProtocolStorageRequest Request, OpenIddictServerEvents.ProcessSignInContext SignIn,
        AdministratorPassthrough? Passthrough, ProtocolResponseOutput Output);
}
