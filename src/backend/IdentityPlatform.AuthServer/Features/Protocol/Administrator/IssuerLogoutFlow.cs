using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Owns one actual native validation, explicit confirmation and immutable post-cleanup logout response.</summary>
internal sealed class IssuerLogoutFlow
{
    private readonly OpenIddictServerEvents.ValidateEndSessionRequestContext validation;
    private readonly ProtocolStorageRequest request;
    private readonly HttpContext http;
    private readonly string parameters;
    private readonly string destination;
    private readonly OpenIddictRequest original;
    private OpenIddictServerEvents.HandleEndSessionRequestContext? handling;
    private OpenIddictServerEvents.ProcessSignOutContext? signOut;
    private ProtocolResponseOutput? output;
    private JourneyLogout.Receipt? receipt;
    private IssuerLogoutResponse? formatted;
    private bool returned;

    /// <summary>Captures only the completed real validation on its original bounded request and native transaction.</summary>
    private IssuerLogoutFlow(OpenIddictServerEvents.ValidateEndSessionRequestContext validation)
    {
        this.validation = validation;
        http = validation.Transaction.GetHttpRequest()!.HttpContext;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        parameters = IssuerLogoutInput.Digest(validation.Request);
        destination = validation.PostLogoutRedirectUri ?? throw ProtocolStorageOwner.Unavailable();
        original = validation.Request;
    }

    /// <summary>Records native provenance after all installed validation handlers return; rejection never creates a receipt.</summary>
    internal static async ValueTask ValidateAsync(OpenIddictServerEvents.ValidateEndSessionRequestContext context, Func<ValueTask> native)
    {
        var original = context.Request;
        var parameters = IssuerLogoutInput.Digest(original);
        await native();
        if (!context.IsRejected && !context.IsRequestHandled && !context.IsRequestSkipped)
        {
            if (Find(context.Transaction) is not null || !ReferenceEquals(original, context.Request) ||
                parameters != IssuerLogoutInput.Digest(context.Request))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            context.Transaction.SetProperty(typeof(IssuerLogoutFlow).FullName!, new IssuerLogoutFlow(context));
        }
    }

    /// <summary>Returns only the private receipt stored by the actual completed native validation.</summary>
    internal static IssuerLogoutFlow? Find(OpenIddictServerTransaction transaction) => transaction.GetProperty<IssuerLogoutFlow>(typeof(IssuerLogoutFlow).FullName!);

    /// <summary>Stages confirmation or native sign-out in a child, then releases its immutable bytes only after actual scope cleanup.</summary>
    internal async ValueTask RunAsync(OpenIddictServerEvents.HandleEndSessionRequestContext context)
    {
        RequireCurrent(context);
        if (handling is not null || context.IsRequestHandled || context.IsRequestSkipped || context.IsRejected)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        handling = context;
        var result = await ProtocolResponseChild.RunAsync(http, http.RequestServices.GetRequiredService<IServiceScopeFactory>(), async (scope, capture, token) =>
        {
            await HandleAsync(scope, token);
            await capture.SealAsync();
            RequireCurrent(context);
            receipt?.RequireCurrent(request);
            formatted?.RequireCompleted();
            return true;
        });
        RequireCurrent(context);
        receipt?.RequireCurrent(request);
        formatted?.RequireCompleted(activeCapture: false);
        output = result.Output ?? throw ProtocolStorageOwner.Unavailable();
        await output.CopyAndSealParentAsync(context.CancellationToken);
        context.HandleRequest();
        returned = true;
    }

    /// <summary>Uses exact current named-cookie authority and observes the fixed manifest without claiming profile-disable serialization.</summary>
    private async Task HandleAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Require(ProtocolStorageMode.Owned);
        var policy = scope.Services.GetRequiredService<AdministratorRegistrationPolicy>();
        var registration = await AdministratorRegistration.ObserveLogoutAsync(scope, policy, token);
        var candidate = await AdministratorSecurityBrowser.CandidateAsync(http);
        if (registration is null || candidate is null || candidate.SessionId != IssuerLogoutInput.Hint(validation.Request) ||
            destination != policy.Application().PostLogoutRedirectUris.Single().AbsoluteUri ||
            http.Items[AdministratorCookieEvents.StatusKey] is not JourneySession session)
        {
            await IssuerLogoutPage.IncompleteAsync(http);
            return;
        }
        var confirmation = scope.Services.GetRequiredService<IssuerLogoutConfirmation>();
        var antiforgery = scope.Services.GetRequiredService<IAntiforgery>();
        if (HttpMethods.IsGet(http.Request.Method))
        {
            await IssuerLogoutPage.ConfirmAsync(http, validation.Request, confirmation.Create(validation.Request, session),
                antiforgery.GetAndStoreTokens(http).RequestToken ?? throw ProtocolStorageOwner.Unavailable());
            return;
        }
        var form = await http.Request.ReadFormAsync(token);
        var expires = confirmation.Read(form[IssuerLogoutInput.Confirmation], validation.Request, candidate);
        if (expires is null || !await antiforgery.IsRequestValidAsync(http))
        {
            await IssuerLogoutPage.IncompleteAsync(http);
            return;
        }
        await RevokeAsync(scope, candidate, expires.Value, registration, policy, token);
    }

    /// <summary>Reuses the actual serialized account ceremony; native output can follow only a confirmed, disposed commit.</summary>
    private async Task RevokeAsync(ProtocolStorageScope scope, JourneySessionCandidate candidate, JourneyLogoutDeadline expires,
        AdministratorRegistrationBinding observed, AdministratorRegistrationPolicy policy, CancellationToken token)
    {
        RequireCurrent(handling!);
        var current = await AdministratorRegistration.ObserveLogoutAsync(scope, policy, token);
        if (current != observed)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        receipt = await scope.Services.GetRequiredService<JourneyLogout>().RevokeForProtocolAsync(candidate, request, expires, token)
            ?? throw ProtocolStorageOwner.Unavailable();
        receipt.RequireCurrent(request);
        await http.SignOutAsync(AccountSchemes.Administrator);
        await http.SignOutAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        RequireCurrent(handling!);
        receipt.RequireCurrent(request);
        if (signOut is not { IsRequestHandled: true, IsRequestSkipped: false, IsRejected: false } || formatted is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        formatted.RequireCompleted();
    }

    /// <summary>Observes the actual native sign-out once, only inside the committed ceremony's original handling scope.</summary>
    internal async ValueTask SignOutAsync(OpenIddictServerEvents.ProcessSignOutContext context, Func<ValueTask> native)
    {
        RequireCurrent(context);
        if (receipt is null || signOut is not null || handling is null || returned || context.IsRequestHandled || context.IsRequestSkipped || context.IsRejected)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        receipt.RequireCurrent(request);
        signOut = context;
        formatted = new(context, destination, original.State!);
        await native();
        RequireCurrent(context);
        formatted.RequireCompleted();
    }

    /// <summary>Delegates only the genuine installed native query formatter after the private sign-out continuation was claimed.</summary>
    internal ValueTask FormatAsync(OpenIddictServerEvents.ApplyEndSessionResponseContext context, Func<ValueTask> native) =>
        formatted?.ObserveAsync(context, native) ?? throw ProtocolStorageOwner.Unavailable();

    /// <summary>Requires exact native validation identity and immutable original fields throughout nested callbacks and cleanup.</summary>
    private void RequireCurrent(OpenIddictServerEvents.BaseContext context)
    {
        request.RequireCurrent();
        if (!ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) || !ReferenceEquals(context.Transaction, validation.Transaction) ||
            !ReferenceEquals(context.Transaction.GetHttpRequest()?.HttpContext, http) || validation.IsRejected || validation.IsRequestHandled || validation.IsRequestSkipped ||
            context.EndpointType != OpenIddictServerEndpointType.EndSession || !ReferenceEquals(original, context.Transaction.Request) ||
            validation.PostLogoutRedirectUri != destination || validation.ClientId != original.ClientId ||
            !ReferenceEquals(validation, context.Transaction.GetProperty<OpenIddictServerEvents.ValidateEndSessionRequestContext>(typeof(OpenIddictServerEvents.ValidateEndSessionRequestContext).FullName!)) ||
            parameters != IssuerLogoutInput.Digest(validation.Request))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Admits exactly one frozen response after actual native outer completion; no generic handled flag grants publication.</summary>
    internal ProtocolResponseOutcome Claim(OpenIddictServerEvents.ProcessRequestContext outer)
    {
        RequireCurrent(outer);
        if (!returned || handling is not { IsRequestHandled: true, IsRequestSkipped: false, IsRejected: false } ||
            !outer.IsRequestHandled || outer.IsRequestSkipped || outer.IsRejected || output is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        returned = false;
        output.RequireFrozenParent();
        if (receipt is null)
        {
            return signOut is null ? ProtocolResponseOutcome.ReadOnly : throw ProtocolStorageOwner.Unavailable();
        }
        formatted!.RequireCompleted(activeCapture: false);
        request.AdmitIssuerLogout(output, receipt);
        return ProtocolResponseOutcome.VerifiedIssuerLogout;
    }
}
