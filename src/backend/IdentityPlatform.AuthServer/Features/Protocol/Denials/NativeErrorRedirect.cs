using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Denials;

/// <summary>Freezes successful actual native redirect validation before the later error pipeline reads mutable transaction properties.</summary>
internal sealed class NativeErrorRedirect
{
    private readonly OpenIddictServerEvents.ValidateAuthorizationRequestContext validation;
    private readonly OpenIddictRequest request;
    private readonly string parameters;
    private readonly string? callback;
    private readonly string? issuer;

    /// <summary>Records exact identities and values only after actual native validation returns normally.</summary>
    internal NativeErrorRedirect(OpenIddictServerEvents.ValidateAuthorizationRequestContext validation)
    {
        this.validation = validation;
        request = validation.Request;
        parameters = Parameters(request);
        callback = validation.RedirectUri;
        issuer = (validation.Options.Issuer ?? validation.BaseUri)?.AbsoluteUri;
    }

    /// <summary>Rejects mutated/replaced validation, request, issuer or callback, including a substitute non-rejected context.</summary>
    internal bool Allows(OpenIddictServerEvents.ApplyAuthorizationResponseContext context, string expected) =>
        ReferenceEquals(context.Transaction, validation.Transaction) && ReferenceEquals(context.Request, request) &&
        ReferenceEquals(context.Transaction.GetProperty<OpenIddictServerEvents.ValidateAuthorizationRequestContext>(
            typeof(OpenIddictServerEvents.ValidateAuthorizationRequestContext).FullName!), validation) &&
        !validation.IsRejected && !validation.IsRequestHandled && !validation.IsRequestSkipped &&
        callback == expected && validation.RedirectUri == callback && context.RedirectUri == callback &&
        issuer == (context.Options.Issuer ?? context.BaseUri)?.AbsoluteUri && parameters == Parameters(request);

    /// <summary>Detects state and every native request mutation without exposing credential values.</summary>
    private static string Parameters(OpenIddictRequest value) => FactorBinding.Digest(value.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, Value = pair.Value.ToString() }).ToArray());
}
