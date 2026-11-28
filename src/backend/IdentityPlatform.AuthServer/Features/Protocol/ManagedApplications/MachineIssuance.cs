using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Owns the one native access token and application-actor audit written by a client-credentials exchange.</summary>
internal sealed class MachineIssuance(OpenIddictServerEvents.ProcessSignInContext context, MachineGrant grant)
{
    private const string Issued = "machine-token-issued";
    private const string Application = "application";
    private const string Accepted = "accepted";
    private const int ResponseFields = 3;
    private NativeIssuedToken? entry;
    private bool generating;
    private readonly NativeResponseCompletion formatter = new(context, string.Empty, current => ResponseMatches(current, grant));

    private const string NoStore = "no-store";

    /// <summary>Finds only the current coordinated machine operation on this native transaction.</summary>
    internal static MachineIssuance? Find(OpenIddictServerTransaction transaction) =>
        transaction.GetProperty<MachineIssuance>(typeof(MachineIssuance).FullName!);

    /// <summary>Serializes credential and catalogue authorization with native signing, storage verification and the atomic audit.</summary>
    internal async ValueTask RunAsync(ProtocolStorageScope scope, ProtocolResponseChild response, CancellationToken token)
    {
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await ManagedRegistration.LockAsync(scope, token);
        await RequireAuthorityAsync(scope, token);
        var now = scope.Owner.Clock.GetUtcNow();
        var cutoff = now.ToUnixTimeSeconds();
        var budget = await ManagedNativeBudget.ReadAsync(scope, cutoff, token);
        if (budget.Tokens >= ManagedLoginRules.TokenLimit || budget.Authorizations > ManagedLoginRules.AuthorizationLimit)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Transaction.SetProperty(typeof(MachineIssuance).FullName!, this);
        try
        {
            var dispatcher = scope.Services.GetRequiredService<OpenIddictServerDispatcher>();
            await ProtocolStorageDispatch.RunAsync(scope.Owner, () => context.CancellationToken,
                current => context.CancellationToken = current, () => dispatcher.DispatchAsync(context));
            await response.SealAsync();
            var tokenId = context.AccessTokenPrincipal?.GetTokenId() ?? throw ProtocolStorageOwner.Unavailable();
            await grant.References.SaveAsync(scope, tokenId, token);
            await grant.ObserveAsync(scope, token);
            await RecordAsync(scope, token);
            await RequireAuthorityAsync(scope, token);
            await VerifyAsync(scope, budget, cutoff, token);
            await transaction.CommitAsync(token);
        }
        finally { context.Transaction.SetProperty<MachineIssuance>(typeof(MachineIssuance).FullName!, null); }
    }

    /// <summary>Caps the real prepared access token at the credential deadline and verifies the allowed principal before signing.</summary>
    internal void Bound()
    {
        var prepared = context.AccessTokenPrincipal;
        if (!context.GenerateAccessToken || !context.IncludeAccessToken || context.GenerateIdentityToken || context.GenerateAuthorizationCode ||
            context.GenerateRefreshToken || context.GenerateDeviceCode || context.GenerateUserCode || context.GenerateRequestToken ||
            prepared?.GetCreationDate() is not { } created || prepared.GetExpirationDate() is not { } expires ||
            prepared.GetAuthorizationId() is not null || prepared.GetClaim(OpenIddictConstants.Claims.Subject) != grant.Subject ||
            prepared.GetClaim(MachineClientRules.IdentityType) != MachineClientRules.Machine || !prepared.GetAudiences().ToHashSet(StringComparer.Ordinal).SetEquals(grant.Resources) ||
            !prepared.GetScopes().ToHashSet(StringComparer.Ordinal).SetEquals(grant.Scopes) || prepared.GetPresenters() is not [var client] || client != grant.Registration.ClientId)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var deadline = DateTimeOffset.FromUnixTimeSeconds(Math.Min(grant.Registration.ExpiresAt, expires.ToUnixTimeSeconds()));
        if (deadline <= created)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        prepared.SetExpirationDate(deadline);
        ProtocolStorageAmbient.Current!.Request.RestrictPublication(deadline);
    }

    /// <summary>Observes the native generator and its actual signed access token, rejecting additional or nested token generation.</summary>
    internal async ValueTask GenerateAsync(OpenIddictServerEvents.GenerateTokenContext current, Func<ValueTask> native)
    {
        if (!MachinePrincipal.Prepared(context, grant) || entry is not null || current.TokenType != OpenIddictConstants.TokenTypeIdentifiers.AccessToken ||
            !ReferenceEquals(current.Transaction, context.Transaction) || !ReferenceEquals(current.Principal, context.AccessTokenPrincipal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        entry = new NativeIssuedToken(current, grant.Registration.ClientId);
        generating = true;
        try
        {
            await native();
            entry.Complete();
        }
        finally { generating = false; }
    }

    /// <summary>Reuses native key selection, persistence and signature observations scoped to this single generator invocation.</summary>
    internal async ValueTask ObserveAsync(OpenIddictServerEvents.GenerateTokenContext current, Func<NativeIssuedToken, ValueTask> callback)
    {
        if (!generating || entry is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        entry.RequireContext(current);
        await callback(entry);
    }

    /// <summary>Compares the sealed native output with one freshly read machine token and the original aggregate budgets.</summary>
    private async ValueTask VerifyAsync(ProtocolStorageScope scope, ManagedNativeBudget before, long cutoff, CancellationToken token)
    {
        var after = await ManagedNativeBudget.ReadAsync(scope, cutoff, token);
        if (!formatter.IsCurrent() || !ResponseMatches(context, grant) || !context.IsRequestHandled || context.IsRejected || context.IsRequestSkipped || after != (before with
        {
            Tokens = before.Tokens + 1
        }) ||
            entry is null || !entry.Matches(context.AccessTokenPrincipal, context.AccessToken) ||
            !await entry.VerifyAsync(scope, scope.Services.GetRequiredService<IOpenIddictTokenManager>(), grant.Registration.ApplicationId, token) ||
            context.Transaction.GetHttpRequest()!.HttpContext.Features.Get<IHttpResponseFeature>() is not ProtocolResponseCapture capture ||
            !WireMatches(capture) || !await grant.References.MatchesAsync(scope, context.AccessTokenPrincipal!.GetTokenId()!, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Observes completion of the actual native JSON formatter on this transaction.</summary>
    internal ValueTask FormatAsync(OpenIddictServerEvents.BaseRequestContext current, Func<ValueTask> native) => formatter.ObserveAsync(current, native);

    /// <summary>Checks the exact response vocabulary and binds its advertised lifetime to the prepared signed credential.</summary>
    private static bool ResponseMatches(OpenIddictServerEvents.ProcessSignInContext current, MachineGrant bound) =>
        MachinePrincipal.Prepared(current, bound) && current.EndpointType == OpenIddictServerEndpointType.Token &&
        current.Response.GetParameters().Count == ResponseFields && current.Response.AccessToken == current.AccessToken && !string.IsNullOrEmpty(current.AccessToken) &&
        current.Response.TokenType == OpenIddictConstants.TokenTypes.Bearer && current.Response.ExpiresIn is > 0 and <= AdministratorRegistrationPolicy.TokenLifetimeSeconds &&
        current.Response.ExpiresIn <= Math.Ceiling((current.AccessTokenPrincipal!.GetExpirationDate()!.Value - current.AccessTokenPrincipal!.GetCreationDate()!.Value).TotalSeconds);

    /// <summary>Requires a bearer access token and bounded lifetime, without an ID token, refresh token, cookie or redirect.</summary>
    private bool WireMatches(ProtocolResponseCapture capture)
    {
        if (!capture.IsSealed || capture.StatusCode != StatusCodes.Status200OK || capture.Headers.SetCookie.Count != 0 ||
            capture.Headers.Location.Count != 0 || capture.Headers.AccessControlAllowOrigin.Count != 0 || capture.Headers.CacheControl != NoStore)
        {
            return false;
        }
        using var document = JsonDocument.Parse(capture.Bytes);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Count() == ResponseFields &&
            root.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out var access) && access.GetString() == context.AccessToken &&
            root.TryGetProperty(OpenIddictConstants.Parameters.TokenType, out var type) && type.GetString() == OpenIddictConstants.TokenTypes.Bearer &&
            root.TryGetProperty(OpenIddictConstants.Parameters.ExpiresIn, out var lifetime) && lifetime.TryGetInt64(out var seconds) &&
            seconds == context.Response.ExpiresIn;
    }

    /// <summary>Rechecks the authenticated native identity, credential revision and exact catalogue binding under the same transaction.</summary>
    private async ValueTask RequireAuthorityAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (!await grant.MatchesAsync(scope, (await MachineGrant.ReadAsync(scope, context.Request, token)).Grant, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Persists the application actor and target alongside the signed token, with no human identity or credential data.</summary>
    private async ValueTask RecordAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var audit = new SecurityAuditIntent
        {
            Event = Issued,
            ActorType = MachineClientRules.Machine,
            ActorId = grant.Registration.ApplicationId,
            TargetType = Application,
            TargetId = grant.Registration.ApplicationId,
            Outcome = Accepted,
            OccurredAt = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds()
        };
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(audit);
            await scope.Owner.Database.SaveChangesAsync(current);
            if (!await scope.Owner.Database.Set<SecurityAuditIntent>().AsNoTracking().AnyAsync(row => row.Id == audit.Id && row.Event == Issued &&
                row.ActorType == MachineClientRules.Machine && row.ActorId == grant.Registration.ApplicationId && row.TargetId == grant.Registration.ApplicationId &&
                row.TargetType == Application && row.Outcome == Accepted && row.OccurredAt == audit.OccurredAt, current))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }, token, write: true);
    }
}
