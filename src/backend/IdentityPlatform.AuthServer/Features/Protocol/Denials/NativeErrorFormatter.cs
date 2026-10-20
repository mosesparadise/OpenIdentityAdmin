using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Observes actual installed native error serialization and freezes its safe output before child cleanup.</summary>
internal sealed class NativeErrorFormatter(OpenIddictServerEvents.BaseRequestContext source, string expected, AdministratorRegistrationPolicy policy)
{
    private OpenIddictServerEvents.BaseRequestContext? completed;
    private ProtocolResponseCapture? capture;
    private readonly OpenIddictResponse? response = source.Transaction.Response;
    private string? parameters;
    private string? output;
    private readonly Dictionary<string, string?[]> baseline = Capture(source).Headers
        .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    private bool headersChecked;

    /// <summary>Finds only the coordinator's currently active formatter proof on the original native transaction.</summary>
    internal static NativeErrorFormatter? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<NativeErrorFormatter>(typeof(NativeErrorFormatter).FullName!);

    /// <summary>Removes only the default URI added by the actual native challenge handler after verifying the private finite endpoint denial.</summary>
    internal async ValueTask NormalizeChallengeAsync(OpenIddictServerEvents.ProcessChallengeContext context, Func<ValueTask> native)
    {
        if (!ReferenceEquals(source, context) || completed is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        RequireParameters(context);
        await native();
        var expectedUri = context.EndpointType == OpenIddictServerEndpointType.Authorization ? NativeErrorPolicy.AuthorizationDefaultUri : NativeErrorPolicy.TokenDefaultUri;
        if (context.Response.ErrorUri != expectedUri)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Response.ErrorUri = null;
        RequireParameters(context);
    }

    /// <summary>Validates before native header attachment, which otherwise serializes arbitrary scalar response parameters.</summary>
    internal void BeforeHeaders(OpenIddictServerEvents.BaseRequestContext context)
    {
        RequireParameters(context);
        var current = Capture(context);
        if (completed is not null || headersChecked || current.HasStarted || !current.Bytes.IsEmpty)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (baseline.Count != current.Headers.Count || baseline.Any(pair => !current.Headers.TryGetValue(pair.Key, out var values) || !values.ToArray().SequenceEqual(pair.Value)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        headersChecked = true;
    }

    /// <summary>Observes only the endpoint's genuine JSON, fixed query or local error formatter, never an arbitrary handled flag.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.BaseRequestContext context, NativeErrorFormat format, Func<ValueTask> native)
    {
        RequireParameters(context);
        var current = Capture(context);
        if (completed is not null || !headersChecked || context.IsRequestHandled || context.IsRequestSkipped || current.HasStarted || !current.Bytes.IsEmpty ||
            !Expected(context, format))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var before = Parameters(context);
        await native();
        RequireParameters(context);
        if (!context.IsRequestHandled || context.IsRequestSkipped || !ReferenceEquals(current, Capture(context)) || before != Parameters(context) ||
            !NativeErrorHeaders.Allows(current, baseline, context, format))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        completed = context;
        capture = current;
        parameters = before;
        output = Output(current);
    }

    /// <summary>Rechecks all native and HTTP output after callbacks/producers have sealed and again after actual child cleanup.</summary>
    internal void RequireCompleted(bool activeCapture)
    {
        if (completed is not { IsRequestHandled: true, IsRequestSkipped: false } || capture is null ||
            !source.IsRequestHandled || source.IsRequestSkipped || source is OpenIddictServerEvents.BaseValidatingContext { IsRejected: true } || !ReferenceEquals(response, source.Transaction.Response) ||
            parameters != Parameters(completed) || activeCapture && (output != Output(capture) || !ReferenceEquals(capture, Capture(completed))))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        RequireParameters(completed);
    }

    /// <summary>Requires the same source transaction and an allowed response before each formatter-related boundary.</summary>
    private void RequireParameters(OpenIddictServerEvents.BaseRequestContext context)
    {
        if (!ReferenceEquals(context.Transaction, source.Transaction) || !ReferenceEquals(response, context.Transaction.Response) || !NativeErrorPolicy.Parameters(context, expected, policy))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Restricts each observer to its actual installed endpoint formatter; invalid authorization redirects remain local.</summary>
    private static bool Expected(OpenIddictServerEvents.BaseRequestContext context, NativeErrorFormat format) => (context, format) switch
    {
        (OpenIddictServerEvents.ApplyTokenResponseContext, NativeErrorFormat.Json) => true,
        (OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext, NativeErrorFormat.Json) => true,
        (OpenIddictServerEvents.ApplyAuthorizationResponseContext authorization, NativeErrorFormat.Query) => !string.IsNullOrEmpty(authorization.RedirectUri),
        (OpenIddictServerEvents.ApplyAuthorizationResponseContext authorization, NativeErrorFormat.Local) => string.IsNullOrEmpty(authorization.RedirectUri),
        _ => false
    };

    /// <summary>Rejects replacement or escape from the original request's bounded response capture.</summary>
    private static ProtocolResponseCapture Capture(OpenIddictServerEvents.BaseRequestContext context)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext;
        var request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        request.RequireCurrent();
        return http?.Features.Get<IHttpResponseFeature>() is ProtocolResponseCapture current && current.BelongsTo(request) &&
            ReferenceEquals(http.Features.Get<IHttpResponseBodyFeature>(), current) ? current : throw ProtocolStorageOwner.Unavailable();
    }

    /// <summary>Detects native response mutation without retaining printable protocol values.</summary>
    private static string Parameters(OpenIddictServerEvents.BaseRequestContext context) => FactorBinding.Digest(context.Transaction.Response!.GetParameters()
        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());

    /// <summary>Includes every staged byte/header/status in the final immutable-output check.</summary>
    private static string Output(ProtocolResponseCapture current) => FactorBinding.Digest(new
    {
        current.StatusCode,
        current.ReasonPhrase,
        Headers = current.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray(),
        Body = Convert.ToHexString(SHA256.HashData(current.Bytes.Span))
    });
}

/// <summary>Names only the three formatters installed on the fixed profile's actual native endpoint descriptors.</summary>
internal enum NativeErrorFormat
{
    Json, Query, Local
}
