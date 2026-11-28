using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Admits cross-origin reads only for an explicitly registered origin on the current public browser client.</summary>
internal static class ManagedBrowserCors
{
    private const string Discovery = "/.well-known/openid-configuration";
    private const string NoStore = "no-store";
    private const string UserInfoHeaders = "Authorization, Content-Type";
    private const string PreflightVary = "Origin, Access-Control-Request-Method, Access-Control-Request-Headers";
    private const int MaximumHeaderLength = 64;

    /// <summary>Confirms the exact serialized origin and a credential-free browser fetch against fresh registration authority.</summary>
    internal static bool Allowed(HttpRequest request, ManagedRegistration registration) =>
        request.Headers.Origin is { Count: 1 } origins && BrowserOriginPolicy.IsOrigin(origins[0]) &&
        registration.Profile == ApplicationEditorRules.BrowserProfile && request.Headers.Cookie.Count == 0 &&
        registration.Origins.Contains(origins[0]!, StringComparer.Ordinal);

    /// <summary>Writes only to the transaction-owned response before the response bytes and headers are sealed.</summary>
    internal static void Publish(HttpContext http, ManagedRegistration registration)
    {
        if (!Allowed(http.Request, registration))
        {
            throw new InvalidOperationException();
        }
        http.Response.Headers.AccessControlAllowOrigin = http.Request.Headers.Origin;
        http.Response.Headers.AppendCommaSeparatedValues(HeaderNames.Vary, HeaderNames.Origin);
    }

    /// <summary>Exposes public discovery and issuer verification keys to the registered browser origins within the normal owned request.</summary>
    internal static async ValueTask<string?> MetadataAsync(HttpContext http, OpenIddictServerEndpointType endpoint, ProtocolStorageScope scope, CancellationToken token)
    {
        var request = http.Request;
        if (!request.IsHttps || request.PathBase.HasValue || request.QueryString.HasValue || !HttpMethods.IsGet(request.Method) ||
            http.Features.Get<IHttpRequestFeature>()?.RawTarget != request.Path.Value ||
            !(endpoint == OpenIddictServerEndpointType.Configuration && request.Path.Value == Discovery ||
            endpoint == OpenIddictServerEndpointType.JsonWebKeySet && request.Path.Value == ProtocolOptions.KeysPath) ||
            request.Headers.Cookie.Count != 0 || request.Headers.Authorization.Count != 0 ||
            request.Headers.Origin is not { Count: 1 } origin || !BrowserOriginPolicy.IsOrigin(origin[0]) ||
            !await RegisteredAsync(scope, origin[0]!, token))
        {
            return null;
        }
        http.Response.Headers.AccessControlAllowOrigin = origin;
        http.Response.Headers.AppendCommaSeparatedValues(HeaderNames.Vary, HeaderNames.Origin);
        return origin[0];
    }

    /// <summary>Verifies the originally granted origin after native formatter callbacks and before publication.</summary>
    internal static void RequireHeaders(HttpContext http, string origin)
    {
        var headers = http.Response.Headers;
        if (headers.AccessControlAllowOrigin.Count != 1 || headers.AccessControlAllowOrigin[0] != origin ||
            headers.ContainsKey(HeaderNames.AccessControlAllowCredentials) || !headers.GetCommaSeparatedValues(HeaderNames.Vary).Contains(HeaderNames.Origin, StringComparer.OrdinalIgnoreCase))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Resolves browser preflights without accepting token material or trusting a client ID unavailable on an OPTIONS request.</summary>
    internal static async ValueTask<bool> PreflightAsync(HttpContext http, ProtocolStorageScope scope, CancellationToken token)
    {
        var request = http.Request;
        if (!HttpMethods.IsOptions(request.Method) || request.Path.Value is not (ProtocolOptions.TokenPath or ProtocolOptions.UserInfoPath))
        {
            return false;
        }
        http.Response.Headers.CacheControl = NoStore;
        var method = request.Headers.AccessControlRequestMethod;
        var userInfo = request.Path.Value == ProtocolOptions.UserInfoPath;
        if (!request.IsHttps || request.PathBase.HasValue || request.QueryString.HasValue ||
            http.Features.Get<IHttpRequestFeature>()?.RawTarget != request.Path.Value ||
            request.Headers.Origin is not { Count: 1 } origin || !BrowserOriginPolicy.IsOrigin(origin[0]) ||
            method.Count != 1 || !(method[0] == HttpMethods.Post || userInfo && method[0] == HttpMethods.Get) ||
            !HeadersAllowed(request, userInfo) || request.Headers.Cookie.Count != 0 || request.Headers.Authorization.Count != 0)
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return true;
        }
        if (!await RegisteredAsync(scope, origin[0]!, token))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return true;
        }
        http.Response.Headers.AccessControlAllowOrigin = origin;
        http.Response.Headers.AccessControlAllowMethods = method;
        http.Response.Headers.AccessControlAllowHeaders = userInfo ? UserInfoHeaders : HeaderNames.ContentType;
        http.Response.Headers.Vary = PreflightVary;
        http.Response.StatusCode = StatusCodes.Status204NoContent;
        return true;
    }

    /// <summary>Uses the origin-first index under the existing request owner, cancellation and publication deadline.</summary>
    private static ValueTask<bool> RegisteredAsync(ProtocolStorageScope scope, string origin, CancellationToken token) =>
        scope.Owner.RunAsync(async current => await (from row in scope.Owner.Database.Set<ApplicationBrowserOrigin>()
                                                     join policy in scope.Owner.Database.Set<ApplicationPolicy>() on row.ApplicationId equals policy.ApplicationId
                                                     where row.Origin == origin && policy.Enabled && !policy.Retired && policy.Profile == ApplicationEditorRules.BrowserProfile
                                                     select row.Origin).AnyAsync(current), token);

    /// <summary>Only permits the headers consumed by the selected protocol endpoint.</summary>
    private static bool HeadersAllowed(HttpRequest request, bool userInfo)
    {
        var requested = request.Headers.AccessControlRequestHeaders;
        return requested.Count == 0 || requested.Count == 1 && requested[0] is { Length: > 0 and <= MaximumHeaderLength } list &&
            list.Split(',', StringSplitOptions.TrimEntries).All(header => header.Equals(HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase) ||
                userInfo && header.Equals(HeaderNames.Authorization, StringComparison.OrdinalIgnoreCase));
    }
}
