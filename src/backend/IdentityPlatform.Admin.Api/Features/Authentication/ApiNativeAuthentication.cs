using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Validation.AspNetCore;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Owns the actual named native validation handler, stores and response staging through complete cleanup.</summary>
internal static class ApiNativeAuthentication
{
    /// <summary>Runs native authentication in a fresh guarded scope and never publishes native response bytes or retains request-scoped managers.</summary>
    internal static async Task<AuthenticateResult> AuthenticateAsync(HttpContext http, ApplicationReadBudget budget, TimeProvider clock)
    {
        if (http.Features.Get<OpenIddictValidationAspNetCoreFeature>() is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var factory = http.RequestServices.GetRequiredService<IServiceScopeFactory>();
        var request = new ProtocolStorageRequest(clock, budget.Token);
        using var capture = new ProtocolResponseCapture(request);
        request.BindResponse(capture);
        var features = new ProtocolResponseFeatures(http, capture);
        _ = http.Request.BodyReader;
        features.Install();
        try
        {
            var result = await request.RunAsync(factory, async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                http.RequestServices = scope.Services;
                http.RequestAborted = token;
                try
                {
                    var authenticated = await InvokeAsync(http, scope);
                    features.RequireIntact(request);
                    await capture.SealAsync();
                    if (!capture.Bytes.IsEmpty || capture.StatusCode != StatusCodes.Status200OK || capture.Headers.Count != 0)
                    {
                        return AuthenticateResult.NoResult();
                    }
                    return authenticated;
                }
                finally { await capture.JoinCallbacksAsync(); }
            });
            budget.RequireCurrent();
            return result;
        }
        finally
        {
            try
            {
                await request.DrainAsync();
            }
            finally { features.Restore(); http.Features.Set<OpenIddictValidationAspNetCoreFeature>(null); }
        }
    }

    /// <summary>Preserves genuine named-scheme processing while owned stores link even omitted native manager cancellation tokens.</summary>
    private static async Task<AuthenticateResult> InvokeAsync(HttpContext http, ProtocolStorageScope scope)
    {
        var result = AuthenticateResult.NoResult();
        await ProtocolStorageDispatch.RunAsync(scope.Owner, () => http.RequestAborted, token => http.RequestAborted = token, async () =>
        {
            var provider = scope.Services.GetRequiredService<IAuthenticationHandlerProvider>();
            var handler = await provider.GetHandlerAsync(http, OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            if (handler is IAuthenticationRequestHandler request && !await request.HandleRequestAsync())
            {
                result = await handler.AuthenticateAsync();
            }
        });
        return result;
    }
}
