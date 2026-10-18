using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Binds one native denial to its actual outer request, known write epochs and terminal continuation.</summary>
internal sealed class NativeErrorOrigin
{
    private static readonly object Key = new();
    private readonly HttpContext http;
    private readonly OpenIddictServerEvents.ProcessRequestContext outer;
    private readonly ProtocolStorageRequest request;
    private readonly List<ProtocolStoragePhase> phases = [];
    private OpenIddictRequest? parameters;
    private string? digest;
    private OpenIddictServerEvents.ProcessSignInContext? signIn;
    private AdministratorPassthrough? passthrough;
    private AuthenticationProperties? denial;
    private OpenIddictServerEvents.BaseRequestContext? error;
    private bool returned;
    private bool issuanceReturned;
    private string? expected;
    private readonly ProtocolResponseCapture capture;
    private readonly string headers;
    private NativeErrorRedirect? redirect;

    /// <summary>Creates a private origin only around the real owned outer dispatch.</summary>
    private NativeErrorOrigin(HttpContext http, OpenIddictServerEvents.ProcessRequestContext outer)
    {
        this.http = http;
        this.outer = outer;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        capture = http.Features.Get<IHttpResponseFeature>() as ProtocolResponseCapture ?? throw ProtocolStorageOwner.Unavailable();
        headers = Headers(capture);
        RequireEmpty();
    }

    /// <summary>Records actual return before ASP.NET creates an error or invokes an application endpoint.</summary>
    internal static async ValueTask TrackAsync(OpenIddictServerEvents.ProcessRequestContext context, Func<ValueTask> native)
    {
        var http = context.Transaction.GetHttpRequest()?.HttpContext ?? throw ProtocolStorageOwner.Unavailable();
        var source = new NativeErrorOrigin(http, context);
        http.Items.Add(Key, source);
        await native();
        source.parameters = context.Transaction.Request;
        source.digest = Parameters(source.parameters);
        source.returned = true;
    }

    /// <summary>Finds only the private original HTTP receipt on the same native transaction.</summary>
    internal static NativeErrorOrigin? Find(OpenIddictServerTransaction transaction)
    {
        var http = transaction.GetHttpRequest()?.HttpContext;
        return http is not null && http.Items.TryGetValue(Key, out var value) && value is NativeErrorOrigin source &&
            ReferenceEquals(source.outer.Transaction, transaction) ? source : null;
    }

    /// <summary>Records only explicit supported transaction-open sites; unknown phases fail the later exact checkpoint.</summary>
    internal static void RecordPhase(OpenIddictServerTransaction transaction, ProtocolStorageOwner owner)
    {
        if (Find(transaction) is { } source)
        {
            source.RequireIdentity(transaction);
            if (source.error is not null)
            {
                throw source.Reject();
            }
            source.phases.Add(owner.Capture(write: true));
        }
    }

    /// <summary>Records the one actual issuance attempt before native handlers can reject or complete it.</summary>
    internal void BeginIssuance(OpenIddictServerEvents.ProcessSignInContext context)
    {
        RequireIdentity(context.Transaction);
        if (signIn is not null || error is not null || denial is not null)
        {
            throw Reject();
        }
        signIn = context;
    }

    /// <summary>Records completion only after the issuance child and its transactional resources have actually cleaned up.</summary>
    internal void EndIssuance(OpenIddictServerEvents.ProcessSignInContext context)
    {
        RequireIdentity(context.Transaction);
        if (!ReferenceEquals(signIn, context) || issuanceReturned)
        {
            throw Reject();
        }
        issuanceReturned = true;
    }

    /// <summary>Mints endpoint denial intent with the exact authentication properties later passed to native Forbid.</summary>
    internal static IResult Deny(HttpContext http, string code)
    {
        var transaction = http.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction ?? throw ProtocolStorageOwner.Unavailable();
        var source = Find(transaction) ?? throw ProtocolStorageOwner.Unavailable();
        source.RequireReturned();
        if (source.denial is not null || source.signIn is not null || source.error is not null || !NativeErrorPolicy.Allows(code))
        {
            throw source.Reject();
        }
        source.expected = code;
        source.denial = new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = code,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = NativeErrorPolicy.Description
        });
        return Results.Forbid(source.denial, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    /// <summary>Claims a single actual native error or explicit endpoint challenge, after completed issuance cleanup.</summary>
    internal ProtocolStorageCheckpoint Claim(OpenIddictServerEvents.BaseRequestContext context)
    {
        RequireReturned();
        RequireIdentity(context.Transaction);
        RequireEmpty();
        if (error is not null || context is OpenIddictServerEvents.BaseValidatingContext { IsRejected: true } || context.IsRequestHandled || context.IsRequestSkipped ||
            context.Transaction.Response is not { } response || response.GetParameters().Count != 0)
        {
            throw Reject();
        }
        if (context is OpenIddictServerEvents.ProcessChallengeContext challenge)
        {
            ClaimChallenge(challenge);
        }
        else if (context is OpenIddictServerEvents.ProcessErrorContext native)
        {
            ClaimError(native);
        }
        else
        {
            throw Reject();
        }
        error = context;
        return request.CaptureErrorCheckpoint(phases);
    }

    /// <summary>Checks the native handler's fresh error against the actual rejected source, including its post-cleanup issuance result.</summary>
    private void ClaimError(OpenIddictServerEvents.ProcessErrorContext context)
    {
        var source = signIn is null ? (OpenIddictServerEvents.BaseValidatingContext)outer : signIn;
        if (denial is not null || !source.IsRejected || source.IsRequestHandled || source.IsRequestSkipped ||
            (signIn is not null && !issuanceReturned) || context.Error != (source.Error ?? OpenIddictConstants.Errors.InvalidRequest) ||
            !NativeErrorPolicy.Allows(context.Error))
        {
            throw Reject();
        }
        expected = context.Error;
        context.ErrorDescription = NativeErrorPolicy.Description;
        context.ErrorUri = null;
    }

    /// <summary>Consumes the real endpoint handoff and exact properties; an arbitrary challenge cannot authorize error publication.</summary>
    private void ClaimChallenge(OpenIddictServerEvents.ProcessChallengeContext context)
    {
        var properties = context.Transaction.GetProperty<AuthenticationProperties>(typeof(AuthenticationProperties).FullName!);
        if (denial is null || signIn is not null || !ReferenceEquals(denial, properties) || properties.Items.Count != NativeErrorPolicy.DenialPropertyCount ||
            properties.Parameters.Count != 0 || properties.Items[OpenIddictServerAspNetCoreConstants.Properties.Error] != expected ||
            properties.Items[OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] != NativeErrorPolicy.Description)
        {
            throw Reject();
        }
        passthrough = AdministratorPassthrough.ClaimChallenge(context);
    }

    /// <summary>Preserves exact private challenge properties through native handlers and callbacks, including fields not copied to output.</summary>
    private bool DenialUnchanged(OpenIddictServerEvents.ProcessChallengeContext context) => denial is not null &&
        ReferenceEquals(context.Transaction.GetProperty<AuthenticationProperties>(typeof(AuthenticationProperties).FullName!), denial) &&
        denial.Items.Count == NativeErrorPolicy.DenialPropertyCount && denial.Parameters.Count == 0 &&
        denial.Items.TryGetValue(OpenIddictServerAspNetCoreConstants.Properties.Error, out var code) && code == expected &&
        denial.Items.TryGetValue(OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription, out var description) && description == NativeErrorPolicy.Description;

    /// <summary>Rechecks terminal native formatting and the unchanged original source after every awaited boundary.</summary>
    internal void RequireCompleted(OpenIddictServerEvents.BaseRequestContext context, bool parent = true)
    {
        RequireReturned();
        RequireIdentity(context.Transaction);
        if (context is OpenIddictServerEvents.ProcessErrorContext native && (native.Error != expected || native.ErrorDescription != NativeErrorPolicy.Description || native.ErrorUri is not null) ||
            !ReferenceEquals(error, context) || !context.IsRequestHandled || context.IsRequestSkipped || context is OpenIddictServerEvents.BaseValidatingContext { IsRejected: true } ||
            (context is OpenIddictServerEvents.ProcessChallengeContext challenge ? passthrough?.AllowsChallenge(challenge, parent) != true || !DenialUnchanged(challenge) :
                signIn is { } issuance ? !issuanceReturned || !issuance.IsRejected || issuance.IsRequestHandled || issuance.IsRequestSkipped || (issuance.Error ?? OpenIddictConstants.Errors.InvalidRequest) != expected :
                !outer.IsRejected || outer.IsRequestHandled || outer.IsRequestSkipped || (outer.Error ?? OpenIddictConstants.Errors.InvalidRequest) != expected))
        {
            throw Reject();
        }
    }

    /// <summary>Freezes only the real completed native validation, before any later handler can substitute its cached context.</summary>
    internal async ValueTask ObserveValidationAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context, Func<ValueTask> native)
    {
        RequireIdentity(context.Transaction);
        if (redirect is not null || returned || error is not null)
        {
            throw Reject();
        }
        await native();
        RequireIdentity(context.Transaction);
        if (!context.IsRejected && !context.IsRequestHandled && !context.IsRequestSkipped)
        {
            redirect = new(context);
        }
    }

    /// <summary>Requires exact successful native validation provenance, not merely a mutable transaction property.</summary>
    internal bool AllowsRedirect(OpenIddictServerEvents.ApplyAuthorizationResponseContext context, string callback) =>
        redirect?.Allows(context, callback) == true;

    /// <summary>Rejects preexisting output and substitutions before an error child can replace the original parent response.</summary>
    private void RequireEmpty()
    {
        request.RequireCurrent();
        if (!capture.BelongsTo(request) || !ReferenceEquals(http.Features.Get<IHttpResponseFeature>(), capture) ||
            !ReferenceEquals(http.Features.Get<IHttpResponseBodyFeature>(), capture) || capture.HasStarted || !capture.Bytes.IsEmpty || headers != Headers(capture))
        {
            throw Reject();
        }
    }

    /// <summary>Preserves the exact root host headers/status through the original native request and issuance denial.</summary>
    private static string Headers(ProtocolResponseCapture value) => FactorBinding.Digest(new
    {
        value.StatusCode,
        value.ReasonPhrase,
        Headers = value.Headers.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => new { pair.Key, Values = pair.Value.ToArray() }).ToArray()
    });

    internal string Expected => expected ?? throw Reject();

    /// <summary>Requires the exact post-validation request object and values recorded when the actual outer dispatch returned.</summary>
    private void RequireReturned()
    {
        RequireIdentity(outer.Transaction);
        if (!returned || !ReferenceEquals(parameters, outer.Transaction.Request) || digest != Parameters(parameters))
        {
            throw Reject();
        }
    }

    /// <summary>Rejects substitution of HTTP/native/original request identities even when scoped services are replaced legitimately.</summary>
    private void RequireIdentity(OpenIddictServerTransaction transaction)
    {
        request.RequireCurrent();
        if (!ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) || !ReferenceEquals(transaction, outer.Transaction) ||
            !ReferenceEquals(transaction.GetHttpRequest()?.HttpContext, http))
        {
            throw Reject();
        }
    }

    /// <summary>Detects mutation without exposing protocol values.</summary>
    private static string Parameters(OpenIddictRequest? value) => FactorBinding.Digest(value?.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());

    /// <summary>Latches failed origin checks so later caught exceptions cannot recover publication authority.</summary>
    private InvalidOperationException Reject()
    {
        request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
}
