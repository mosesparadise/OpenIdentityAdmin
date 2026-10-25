using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Rejects ambiguous fixed-profile input before native extraction/authentication or assertion parsing can begin.</summary>
internal static class AdministratorTransportGuard
{
    private const string FormType = "application/x-www-form-urlencoded";
    private static readonly string[] AuthenticationFields = [OpenIddictConstants.Parameters.ClientId, OpenIddictConstants.Parameters.ClientAssertion,
        OpenIddictConstants.Parameters.ClientAssertionType, OpenIddictConstants.Parameters.ClientSecret];

    /// <summary>Runs after shared body/form/time/rate limits and native endpoint inference, including native-owned PAR.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ProcessRequestContext context, ProtocolHostRequest host)
    {
        if (context.EndpointType == OpenIddictServerEndpointType.EndSession)
        {
            var http = context.Transaction.GetHttpRequest()!.HttpContext;
            if (http.RequestServices.GetService<IssuerLogoutEnabled>() is null || context.Options.Issuer is not { } issuer ||
                !await IssuerLogoutInput.AllowsAsync(http, issuer, context.CancellationToken))
            {
                context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            }
            return;
        }
        if (host.IsPilot || context.EndpointType is not (OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token or
            OpenIddictServerEndpointType.PushedAuthorization))
        {
            return;
        }
        var request = context.Transaction.GetHttpRequest()!;
        IEnumerable<KeyValuePair<string, StringValues>>? fields = null;
        if (context.EndpointType == OpenIddictServerEndpointType.Authorization && HttpMethods.IsGet(request.Method))
        {
            fields = request.Query;
        }
        else if (HttpMethods.IsPost(request.Method) && request.Query.Count == 0 && request.HasFormContentType &&
            MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) && contentType.MediaType.Equals(FormType, StringComparison.OrdinalIgnoreCase))
        {
            fields = await request.ReadFormAsync(context.CancellationToken);
        }
        var error = fields is null || request.Headers.Authorization.Count != 0
            ? OpenIddictConstants.Errors.InvalidRequest : Error(fields, context.EndpointType);
        if (error is not null)
        {
            context.Reject(error);
        }
    }

    /// <summary>Preserves scalar cardinality and exact case, forbids caller resource/audience/secret overrides and requires form-bound proof identity.</summary>
    internal static string? Error(IEnumerable<KeyValuePair<string, StringValues>> input, OpenIddictServerEndpointType endpoint)
    {
        var fields = input.ToArray();
        if (fields.Any(pair => pair.Value.Count != 1) || fields.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Length ||
            fields.Any(pair => AuthenticationFields.Any(name => string.Equals(name, pair.Key, StringComparison.OrdinalIgnoreCase) && name != pair.Key)) ||
            fields.Any(pair => pair.Key is OpenIddictConstants.Parameters.Resource or OpenIddictConstants.Parameters.Audience or OpenIddictConstants.Parameters.ClientSecret))
        {
            return OpenIddictConstants.Errors.InvalidRequest;
        }
        var values = fields.ToDictionary(pair => pair.Key, pair => pair.Value[0], StringComparer.Ordinal);
        if (values.GetValueOrDefault(OpenIddictConstants.Parameters.ClientId) != AdministratorProtocolIdentity.ClientId)
        {
            return OpenIddictConstants.Errors.InvalidClient;
        }
        if (endpoint == OpenIddictServerEndpointType.Authorization)
        {
            return !values.ContainsKey(OpenIddictConstants.Parameters.ClientAssertion) && !values.ContainsKey(OpenIddictConstants.Parameters.ClientAssertionType)
                ? null : OpenIddictConstants.Errors.InvalidRequest;
        }
        return values.GetValueOrDefault(OpenIddictConstants.Parameters.ClientAssertion) is { Length: > 0 } &&
            values.GetValueOrDefault(OpenIddictConstants.Parameters.ClientAssertionType) == OpenIddictConstants.ClientAssertionTypes.JwtBearer
                ? null : OpenIddictConstants.Errors.InvalidClient;
    }
}
