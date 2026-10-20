using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Requires the actual selected native formatter and detects any response mutation before issuance commit.</summary>
internal sealed class NativeResponseCompletion(OpenIddictServerEvents.ProcessSignInContext signIn, string callback)
{
    private OpenIddictServerEvents.BaseRequestContext? completed;
    private ProtocolResponseCapture? capture;
    private string? parameters;
    private string? output;

    /// <summary>Observes one real formatter on an empty owned response, requiring native handled completion and the fixed query callback.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.BaseRequestContext context, Func<ValueTask> native)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        var original = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        if (completed is not null || context.IsRequestHandled || context.IsRequestSkipped ||
            !ReferenceEquals(context.Transaction, signIn.Transaction) || !Expected(context) ||
            !NativeAccountResponse.Allows(signIn) || http.Features.Get<IHttpResponseFeature>() is not ProtocolResponseCapture current ||
            !current.BelongsTo(original) || current.HasStarted || !current.Bytes.IsEmpty)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var before = Parameters();
        await native();
        if (!context.IsRequestHandled || context.IsRequestSkipped || before != Parameters() ||
            !ReferenceEquals(http.Features.Get<IHttpResponseFeature>(), current))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        completed = context;
        capture = current;
        parameters = before;
        output = Output(current);
    }

    /// <summary>Checks bytes, headers, status and native parameters again after all child callbacks and producers have been sealed.</summary>
    internal bool IsCurrent() => completed is { IsRequestHandled: true, IsRequestSkipped: false } && capture is not null &&
        signIn.IsRequestHandled && !signIn.IsRejected && !signIn.IsRequestSkipped && parameters == Parameters() && output == Output(capture) &&
        ReferenceEquals(signIn.Transaction.GetHttpRequest()?.HttpContext.Features.Get<IHttpResponseFeature>(), capture);

    /// <summary>Allows only the endpoint's real JSON formatter or its exact approved query redirect.</summary>
    private bool Expected(OpenIddictServerEvents.BaseRequestContext context) => (signIn.EndpointType, context) switch
    {
        (OpenIddictServerEndpointType.PushedAuthorization, OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext) => true,
        (OpenIddictServerEndpointType.Token, OpenIddictServerEvents.ApplyTokenResponseContext) => true,
        (OpenIddictServerEndpointType.Authorization, OpenIddictServerEvents.ApplyAuthorizationResponseContext authorization) =>
            authorization.RedirectUri == callback && authorization.ResponseMode == OpenIddictConstants.ResponseModes.Query,
        _ => false
    };

    /// <summary>Detects mutation of native response values without emitting token or code material to diagnostic output.</summary>
    private string Parameters() => FactorBinding.Digest(signIn.Response.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());

    /// <summary>Binds all bounded staged output, including callback-sensitive redirect headers and the actual native JSON bytes.</summary>
    private static string Output(ProtocolResponseCapture current) => FactorBinding.Digest(new
    {
        current.StatusCode,
        current.ReasonPhrase,
        Headers = current.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray(),
        Body = Convert.ToHexString(SHA256.HashData(current.Bytes.Span))
    });
}
