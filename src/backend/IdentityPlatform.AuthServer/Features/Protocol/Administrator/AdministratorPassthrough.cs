using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Retains the actual native endpoint handoff and binds it once to the later native sign-in.</summary>
internal sealed class AdministratorPassthrough
{
    private static readonly object Key = new();
    private readonly HttpContext http;
    private readonly ProtocolStorageRequest request;
    private readonly ProtocolStorageScope scope;
    private readonly ProtocolResponseCapture capture;
    private readonly string baseline;
    private readonly OpenIddictServerEvents.ProcessRequestContext outer;
    private OpenIddictServerEvents.BaseRequestContext? inner;
    private OpenIddictServerEvents.BaseValidatingContext? signIn;
    private OpenIddictRequest? parameters;
    private string? digest;
    private bool returned;

    /// <summary>Retains the original outer dispatch before the native pipeline determines its endpoint.</summary>
    private AdministratorPassthrough(HttpContext http, OpenIddictServerEvents.ProcessRequestContext outer)
    {
        this.http = http;
        this.outer = outer;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        scope = ProtocolStorageAmbient.Current?.Scope ?? throw ProtocolStorageOwner.Unavailable();
        capture = http.Features.Get<IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
        baseline = Headers(capture);
        RequireEmpty();
    }

    /// <summary>Observes the entire actual request dispatch without interpreting an arbitrary skip as a native handoff.</summary>
    internal static async ValueTask TrackAsync(OpenIddictServerEvents.ProcessRequestContext context, Func<ValueTask> native)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        var receipt = new AdministratorPassthrough(http, context);
        http.Items.Add(Key, receipt);
        try
        {
            await native();
            receipt.returned = true;
            if (receipt.inner is not null && !receipt.IsCurrent())
            {
                throw receipt.Reject();
            }
        }
        catch
        {
            http.Items.Remove(Key);
            receipt.request.Fail();
            throw;
        }
    }

    /// <summary>Records completion only around the installed native passthrough handler, on the unchanged empty parent response.</summary>
    internal static async ValueTask ObserveAsync(OpenIddictServerEvents.BaseRequestContext context, Func<ValueTask> native)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        if (!http.Items.TryGetValue(Key, out var value) || value is not AdministratorPassthrough receipt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        receipt.RequireBefore(context);
        var parameters = context.Transaction.Request ?? throw receipt.Reject();
        var digest = Parameters(parameters);
        await native();
        receipt.RequireEmpty();
        if (!context.IsRequestSkipped || context.IsRequestHandled || !ReferenceEquals(context.Transaction.Request, parameters) ||
            digest != Parameters(parameters) || !receipt.Matches(context))
        {
            throw receipt.Reject();
        }
        receipt.parameters = parameters;
        receipt.digest = digest;
        receipt.inner = context;
    }

    /// <summary>Rejects repeated, foreign or pre-handled attempts before invoking the original native handler.</summary>
    private void RequireBefore(OpenIddictServerEvents.BaseRequestContext context)
    {
        RequireEmpty();
        if (inner is not null || returned || outer.IsRequestHandled || outer.IsRequestSkipped || outer.IsRejected ||
            context.IsRequestHandled || context.IsRequestSkipped || !Matches(context))
        {
            throw Reject();
        }
    }

    /// <summary>Requires an untouched owned response; callbacks remain subject to the later immutable-output checks.</summary>
    private void RequireEmpty()
    {
        request.RequireCurrent();
        if (!OwnsParent() || capture.HasStarted || !capture.Bytes.IsEmpty ||
            baseline != Headers(capture))
        {
            throw Reject();
        }
    }

    /// <summary>Accepts only the matching native authorization or token handling context on the original transaction.</summary>
    private bool Matches(OpenIddictServerEvents.BaseRequestContext context) =>
        ReferenceEquals(context.Transaction, outer.Transaction) && ReferenceEquals(http, context.Transaction.GetHttpRequest()?.HttpContext) &&
        (context.EndpointType, context) is
            (OpenIddictServerEndpointType.Authorization, OpenIddictServerEvents.HandleAuthorizationRequestContext) or
            (OpenIddictServerEndpointType.Token, OpenIddictServerEvents.HandleTokenRequestContext);

    /// <summary>Consumes the private handoff only after the outer native handler has returned to its application endpoint.</summary>
    internal static AdministratorPassthrough Claim(OpenIddictServerEvents.ProcessSignInContext context) => ClaimCore(context);

    /// <summary>Consumes the same real handoff for a privately authorized endpoint denial.</summary>
    internal static AdministratorPassthrough ClaimChallenge(OpenIddictServerEvents.ProcessChallengeContext context) => ClaimCore(context);

    /// <summary>Claims one terminal native continuation without allowing arbitrary base-context substitutions.</summary>
    private static AdministratorPassthrough ClaimCore(OpenIddictServerEvents.BaseValidatingContext context)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        if (!http.Items.Remove(Key, out var value) || value is not AdministratorPassthrough receipt ||
            receipt.signIn is not null || !receipt.IsCurrent() || !ReferenceEquals(context.Transaction, receipt.outer.Transaction) ||
            context.EndpointType != receipt.outer.EndpointType || context.IsRequestHandled || context.IsRequestSkipped || context.IsRejected)
        {
            ProtocolStorageAmbient.Current?.Request.Fail();
            throw ProtocolStorageOwner.Unavailable();
        }
        receipt.RequireEmpty();
        receipt.signIn = context;
        return receipt;
    }

    /// <summary>Rechecks the completed sign-in identity and original handoff after child cleanup and before success publication.</summary>
    internal bool Allows(OpenIddictServerEvents.ProcessSignInContext context) => AllowsCore(context, true);

    /// <summary>Rechecks denial handoff inside its child and again with full parent identity after cleanup.</summary>
    internal bool AllowsChallenge(OpenIddictServerEvents.ProcessChallengeContext context, bool parent) => AllowsCore(context, parent);

    /// <summary>Requires exact terminal identity and preserved handoff; final publication always requires parent ownership.</summary>
    private bool AllowsCore(OpenIddictServerEvents.BaseValidatingContext context, bool parent) => IsCurrent(parent) && ReferenceEquals(signIn, context) &&
        context.IsRequestHandled && !context.IsRequestSkipped && !context.IsRejected &&
        context.EndpointType == outer.EndpointType && ReferenceEquals(context.Transaction, outer.Transaction);

    /// <summary>Detects changes to native handoff contexts and parameters without retaining any scoped service.</summary>
    private bool IsCurrent(bool parent = true) => returned && inner is { IsRequestSkipped: true, IsRequestHandled: false } && Matches(inner) &&
        outer.IsRequestSkipped && !outer.IsRequestHandled && !outer.IsRejected &&
        (!parent || OwnsParent()) && ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && ReferenceEquals(parameters, outer.Transaction.Request) &&
        parameters is not null && digest == Parameters(parameters) &&
        inner is not OpenIddictServerEvents.HandleAuthorizationRequestContext { IsRejected: true } &&
        inner is not OpenIddictServerEvents.HandleTokenRequestContext { IsRejected: true };

    /// <summary>Rejects another child or another response capture even when it shares the same original request budget.</summary>
    private bool OwnsParent() => ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) &&
        ReferenceEquals(scope, ProtocolStorageAmbient.Current?.Scope) && ReferenceEquals(http.RequestServices, scope.Services) &&
        capture.BelongsTo(request) && ReferenceEquals(http.Features.Get<IHttpResponseFeature>(), capture) &&
        ReferenceEquals(http.Features.Get<IHttpResponseBodyFeature>(), capture);

    /// <summary>Hashes native parameters solely for in-request mutation detection.</summary>
    private static string Parameters(OpenIddictRequest request) => FactorBinding.Digest(request.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());

    /// <summary>Preserves the host's existing protection headers while detecting protocol output staged before the native handoff.</summary>
    private static string Headers(ProtocolResponseCapture response) => FactorBinding.Digest(new
    {
        response.StatusCode,
        response.ReasonPhrase,
        Headers = response.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray()
    });

    /// <summary>Latches every invalid handoff so caught exceptions cannot authorize later publication.</summary>
    private InvalidOperationException Reject()
    {
        request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
}
