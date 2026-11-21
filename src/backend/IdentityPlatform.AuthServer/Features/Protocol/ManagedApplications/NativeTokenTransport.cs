using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Binds an exact public native code form to its extracted client before OpenIddict validates the code, redirect and verifier.</summary>
internal sealed class NativeTokenTransport(string clientId)
{
    private const int FieldCount = 5;
    private const int MaximumFieldLength = 4096;
    private const string FormType = "application/x-www-form-urlencoded";

    /// <summary>Admits only credential-free native code transport and preserves the protected administrator and pilot selectors.</summary>
    internal static async ValueTask<bool> TryHandleAsync(OpenIddictServerEvents.ProcessRequestContext context)
    {
        var request = context.Transaction.GetHttpRequest();
        if (context.EndpointType != OpenIddictServerEndpointType.Token || request is null || request.Headers.Authorization.Count != 0 ||
            !HttpMethods.IsPost(request.Method) || !request.HasFormContentType || !MediaTypeHeaderValue.TryParse(request.ContentType, out var media) ||
            !media.MediaType.Equals(FormType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var form = await request.ReadFormAsync(context.CancellationToken);
        var client = form[OpenIddictConstants.Parameters.ClientId];
        if (client.Count != 1 || !ManagedDemand.Candidate(client[0]))
        {
            return false;
        }
        if (!request.IsHttps || request.PathBase.HasValue || request.Path != ProtocolOptions.TokenPath || request.QueryString.HasValue ||
            request.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget != ProtocolOptions.TokenPath || request.Headers.ContentEncoding.Count != 0 ||
            form.Count != FieldCount || form.Files.Count != 0 || form.Any(pair => !Allowed(pair.Key) || pair.Value.Count != 1 ||
                pair.Value[0] is not { Length: > 0 and <= MaximumFieldLength }) ||
            form[OpenIddictConstants.Parameters.GrantType] != OpenIddictConstants.GrantTypes.AuthorizationCode ||
            !ApplicationEditorRules.IsNativeRedirect(form[OpenIddictConstants.Parameters.RedirectUri]))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            return true;
        }
        context.Transaction.SetProperty(typeof(NativeTokenTransport).FullName!, new NativeTokenTransport(client[0]!));
        return true;
    }

    /// <summary>Requires the same client as the bounded original form after native extraction.</summary>
    internal static bool Matches(OpenIddictServerTransaction transaction, string? client) =>
        transaction.GetProperty<NativeTokenTransport>(typeof(NativeTokenTransport).FullName!) is { } receipt && receipt.MatchesClient(client);

    /// <summary>Uses an ordinal client identifier comparison across extraction and registration validation.</summary>
    private bool MatchesClient(string? client) => clientId == client;

    /// <summary>Includes the explicit public client identity and exact RFC 7636 code redemption parameters.</summary>
    private static bool Allowed(string field) => field is OpenIddictConstants.Parameters.ClientId or OpenIddictConstants.Parameters.GrantType or
        OpenIddictConstants.Parameters.Code or OpenIddictConstants.Parameters.RedirectUri or OpenIddictConstants.Parameters.CodeVerifier;
}
