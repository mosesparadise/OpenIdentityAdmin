using IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Devices;

/// <summary>Routes bounded managed device requests before any other grant handler and retains native output through original scope cleanup.</summary>
internal static class DeviceEndpoint
{
    private const int MaximumCode = 4096;
    private const string NoStore = "no-store";
    private const string Form = "application/x-www-form-urlencoded";
    private static readonly string[] IssueFields = [OpenIddictConstants.Parameters.ClientId, OpenIddictConstants.Parameters.Scope];
    private static readonly string[] PollFields = [OpenIddictConstants.Parameters.GrantType, OpenIddictConstants.Parameters.ClientId, OpenIddictConstants.Parameters.DeviceCode];

    /// <summary>Selects only the configured device routes or an actual device grant on the shared token endpoint.</summary>
    internal static async ValueTask<ProtocolResponseOutcome?> TryAsync(HttpContext http, ProtocolStorageScope scope, OpenIddictServerEndpointType endpoint, DevicePublication publication, CancellationToken token)
    {
        if (endpoint == OpenIddictServerEndpointType.EndUserVerification)
        {
            if (HttpMethods.IsGet(http.Request.Method) && Exact(http, DeviceNative.VerificationPath))
            {
                http.Response.Redirect(Accounts.Devices.IndexModel.Path);
            }
            else
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            http.Response.Headers.CacheControl = NoStore;
            await publication.FreezeAsync(http);
            return ProtocolResponseOutcome.ReadOnly;
        }
        if (endpoint is not (OpenIddictServerEndpointType.DeviceAuthorization or OpenIddictServerEndpointType.Token))
        {
            return null;
        }
        var form = HttpMethods.IsPost(http.Request.Method) && MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var media) &&
            string.Equals(media.MediaType.Value, Form, StringComparison.OrdinalIgnoreCase) ? await http.Request.ReadFormAsync(token) : null;
        var poll = endpoint == OpenIddictServerEndpointType.Token;
        if (poll && (form is null || !form[OpenIddictConstants.Parameters.GrantType].Contains(OpenIddictConstants.GrantTypes.DeviceCode, StringComparer.Ordinal)))
        {
            return null;
        }
        var path = poll ? ProtocolOptions.TokenPath : DeviceNative.AuthorizationPath;
        var request = poll ? new OpenIddictRequest { GrantType = OpenIddictConstants.GrantTypes.DeviceCode } : new OpenIddictRequest();
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        var native = await DeviceNative.CreateAsync(scope, endpoint, request, http);
        if (!Exact(http, path) || http.Request.Headers.Origin.Count != 0 || http.Request.Headers.Authorization.Count != 0 ||
            !Read(form, request, poll))
        {
            await DeviceProtocol.ErrorAsync(scope, native, OpenIddictConstants.Errors.InvalidRequest, token);
            await publication.FreezeAsync(http, native);
            return ProtocolResponseOutcome.ReadOnly;
        }
        native.BindBackchannel();
        return await ExecuteAsync(http, scope, native, poll, publication, token);
    }

    /// <summary>Commits the pacing observation before native token validation and commits the matching native effects before publication.</summary>
    private static async ValueTask<ProtocolResponseOutcome> ExecuteAsync(HttpContext http, ProtocolStorageScope scope, DeviceNative native, bool poll, DevicePublication publication, CancellationToken token)
    {
        var request = native.Transaction.Request!;
        var cadence = default(DevicePollReceipt);
        if (poll)
        {
            cadence = await ProtocolStorageChild.RunAsync(scope.Services.GetRequiredService<IServiceScopeFactory>(), (child, current) =>
                DevicePollAdmission.AdmitAsync(child, request.ClientId!, request.DeviceCode!, current));
        }
        else
        {
            await ProtocolStorageChild.RunAsync(scope.Services.GetRequiredService<IServiceScopeFactory>(), async (child, current) =>
            {
                await using var cleanup = await ProtocolStorageTransaction.OpenAsync(child.Owner);
                await ManagedRegistration.LockAsync(child, current);
                if (await DeviceClientRegistration.ReadLockedAsync(child, request.ClientId!, current) is not null)
                {
                    await DeviceGrantCleanup.RunAsync(child, child.Owner.Clock.GetUtcNow().ToUnixTimeSeconds(), native.Transaction.Options.TokenValidationParameters.ClockSkew, current);
                }
                await cleanup.CommitAsync(current);
                return true;
            });
        }
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await ManagedRegistration.LockAsync(scope, token);
        if (poll)
        {
            await DeviceProtocol.ExchangeAsync(scope, native, cadence, token);
        }
        else
        {
            await DeviceProtocol.AuthorizeAsync(scope, native, token);
        }
        await publication.FreezeAsync(http, native);
        await transaction.CommitAsync(token);
        publication.Require();
        return ProtocolResponseOutcome.Committed;
    }

    /// <summary>Requires the literal configured HTTPS target, without query transport, route aliases or credentials.</summary>
    private static bool Exact(HttpContext http, string path) => http.Request.IsHttps && http.Request.PathBase == PathString.Empty && http.Request.Path == path &&
        !http.Request.QueryString.HasValue && http.Features.Get<IHttpRequestFeature>()?.RawTarget == path;

    /// <summary>Copies exact scalar native request parameters only after checking field count, casing and length.</summary>
    private static bool Read(IFormCollection? form, OpenIddictRequest request, bool poll)
    {
        var names = poll ? PollFields : IssueFields;
        if (form is null || form.Count != names.Length || form.Files.Count != 0 || form.Any(item => !names.Contains(item.Key, StringComparer.Ordinal) ||
            item.Value.Count != 1 || item.Value[0] is not { Length: > 0 and <= MaximumCode }) ||
            !ManagedDemand.Candidate(form[OpenIddictConstants.Parameters.ClientId][0]))
        {
            return false;
        }
        request.ClientId = form[OpenIddictConstants.Parameters.ClientId][0];
        if (poll)
        {
            request.DeviceCode = form[OpenIddictConstants.Parameters.DeviceCode][0];
            return form[OpenIddictConstants.Parameters.GrantType][0] == OpenIddictConstants.GrantTypes.DeviceCode;
        }
        request.Scope = form[OpenIddictConstants.Parameters.Scope][0];
        return ManagedRequestedScopes.Read(request.Scope) is not null;
    }
}

/// <summary>Binds the formatter's exact device output to the final original HTTP publication check after all scope disposal.</summary>
internal sealed class DevicePublication
{
    private ManagedResponseFreeze? frozen;

    /// <summary>Seals one output and retains its status, headers and exact body for the parent request.</summary>
    internal async Task FreezeAsync(HttpContext http, DeviceNative? native = null)
    {
        var capture = http.Features.Get<IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
        var expected = new ManagedResponseFreeze(capture);
        await capture.SealAsync();
        expected.Require();
        if (native is not null)
        {
            native.RequireInput();
            DeviceResponse.Require(capture, native);
        }
        if (Interlocked.CompareExchange(ref frozen, expected, null) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Rechecks an admitted native device response before transfer to the live HTTPS client.</summary>
    internal void Require() => frozen?.Require();
}
