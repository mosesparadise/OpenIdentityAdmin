using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Commits actual anonymous native caching in a fresh owned child before releasing any browser proof or self-redirect.</summary>
internal sealed class ManagedCacheCoordinator(IServiceScopeFactory factory)
{
    private static readonly object PublicationKey = new();

    /// <summary>Requires completed real native validation before any write scope or anonymous cache capability is created.</summary>
    internal async ValueTask RunAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var admission = ManagedAdmission.Find(context.Transaction) ?? throw ProtocolStorageOwner.Unavailable();
        var http = context.Transaction.GetHttpRequest()!.HttpContext;
        var original = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        if (admission.Demand.Cached || context.ClientId != admission.Demand.ClientId || context.Principal?.Identity?.IsAuthenticated != false ||
            context.Principal.GetClaim(OpenIddictConstants.Claims.Subject) is not null || context.Principal.GetAuthorizationId() is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var result = await ProtocolResponseChild.RunAsync(http, factory, (scope, response, token) => CacheAsync(scope, response, context, admission, token));
        if (result.Output is not { } output || result.Result <= original.Clock.GetUtcNow().ToUnixTimeSeconds())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await output.CopyAndSealParentAsync(context.CancellationToken);
        http.Items.Add(PublicationKey, new Publication(original, context, output, result.Result));
    }

    /// <summary>Serializes managed admission with editor mutations, reserves before native saves, then seals and verifies before one commit.</summary>
    private static async ValueTask<long> CacheAsync(ProtocolStorageScope scope, ProtocolResponseChild response,
        OpenIddictServerEvents.ProcessSignInContext context, ManagedAdmission admission, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await ManagedRegistration.LockAsync(scope, token);
        var registration = await ManagedRegistration.ReadAsync(scope, admission.Demand.ClientId, token);
        if (!admission.Allows(context.Transaction, registration))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        registration = await registration!.ObserveAsync(scope, token);
        var http = context.Transaction.GetHttpRequest()!.HttpContext;
        var browser = ManagedBrowser.GetOrCreate(http);
        var selector = ManagedLoginRules.CreateSecret();
        var reservation = await ManagedLoginReservation.CreateAsync(scope, registration!, selector, browser, admission.Demand.Digest,
            context.Options.TokenValidationParameters.ClockSkew, token) ?? throw ProtocolStorageOwner.Unavailable();
        ProtocolStorageAmbient.Current!.Request.RestrictPublication(DateTimeOffset.FromUnixTimeSeconds(reservation.ExpiresAt));
        var session = new ManagedCacheSession(scope, context, admission, registration!, reservation, selector);
        context.Transaction.SetProperty(typeof(ManagedCacheSession).FullName!, session);
        try
        {
            var native = scope.Services.GetRequiredService<OpenIddictServerDispatcher>();
            await ProtocolStorageDispatch.RunAsync(scope.Owner, () => context.CancellationToken,
                current => context.CancellationToken = current, () => native.DispatchAsync(context));
            await response.SealAsync();
            await session.VerifyAsync(token);
            await transaction.CommitAsync(token);
            return reservation.ExpiresAt;
        }
        finally
        {
            context.Transaction.SetProperty<ManagedCacheSession>(typeof(ManagedCacheSession).FullName!, null);
            context.Transaction.SetProperty<ManagedCacheProof>(typeof(ManagedCacheProof).FullName!, null);
        }
    }

    /// <summary>Consumes one private cache permit only after actual outer handled completion; endpoint skipped state is never interchangeable.</summary>
    internal static bool Claim(HttpContext http, OpenIddictServerEvents.ProcessRequestContext outer)
    {
        if (!http.Items.Remove(PublicationKey, out var value))
        {
            return false;
        }
        if (value is not Publication permit || !ReferenceEquals(permit.Request, ProtocolStorageAmbient.Current?.Request) ||
            !ReferenceEquals(outer.Transaction, permit.Context.Transaction) || !outer.IsRequestHandled || outer.IsRejected || outer.IsRequestSkipped ||
            !permit.Context.IsRequestHandled || permit.Context.IsRejected || permit.Context.IsRequestSkipped ||
            permit.ExpiresAt <= permit.Request.Clock.GetUtcNow().ToUnixTimeSeconds())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        permit.Output.RequireFrozenParent();
        permit.Request.RequirePublication(ProtocolResponseOutcome.Committed);
        return true;
    }

    /// <summary>Contains only frozen output, original native identities and deadline after all scoped resources have actually disposed.</summary>
    private sealed record Publication(ProtocolStorageRequest Request, OpenIddictServerEvents.ProcessSignInContext Context,
        ProtocolResponseOutput Output, long ExpiresAt);
}
