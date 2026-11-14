using System.Security.Claims;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Owns exact managed source authority, bounded native generation and atomic consent/audit commit after early durable consumption.</summary>
internal sealed class ManagedIssuance(ProtocolStorageScope scope, OpenIddictServerEvents.ProcessSignInContext context) : IAsyncDisposable
{
    private ProtocolStorageTransaction? transaction;
    private ManagedTokenBinding? binding;
    private ManagedLoginInteraction? row;
    private ManagedRegistration? registration;
    private ManagedIssuanceSource? source;
    private ManagedHumanSnapshot? initial;
    private ManagedConsumption? consumption;
    private ManagedIssuanceProof? proof;
    private ManagedNativeBudget budget;
    private long cutoff;
    private string? selector;
    private ClaimsPrincipal? principal;
    private ManagedEmailProjection? email;
    private const string CodeEvent = "managed-code-issued";
    private const string TokenEvent = "managed-token-issued";
    private const string Account = "account";
    private const string Application = "application";
    private const string Accepted = "accepted";
    private const int ExchangeTokens = 2;

    /// <summary>Finds only the coordinator-owned managed issuance state on the original native transaction.</summary>
    internal static ManagedIssuance? Find(OpenIddictServerTransaction transaction) => transaction.GetProperty<ManagedIssuance>(typeof(ManagedIssuance).FullName!);

    /// <summary>Opens after native redemption/restoration; no issuance transaction encloses durable token consumption.</summary>
    internal async ValueTask OpenAsync(CancellationToken token)
    {
        if (transaction is not null || ManagedConsumption.Original(context) is not { } original)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await ManagedRegistration.LockAsync(scope, token);
        binding = await ManagedTokenBinding.ReadAsync(scope, original, context.ClientId!, token) ?? throw ProtocolStorageOwner.Unavailable();
        registration = await binding.Registration.ObserveAsync(scope, token);
        consumption = ManagedConsumption.Find(context.Transaction) ?? throw ProtocolStorageOwner.Unavailable();
        consumption.Claim(scope, context);
        if (!await consumption.VerifyAsync(scope, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        row = await scope.Owner.RunAsync(current => new ValueTask<ManagedLoginInteraction>(scope.Owner.Database.Set<ManagedLoginInteraction>()
            .SingleAsync(value => value.SelectorDigest == binding.Row.SelectorDigest, current)), token);
        if (FactorBinding.Digest(row) != FactorBinding.Digest(binding.Row) || row.State != (Authorization ? ManagedLoginState.Approved : ManagedLoginState.Consumed))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        selector = original.GetClaim(ManagedDemand.SelectorClaim)!;
        principal = context.Principal;
        RequirePrincipal();
        source = await ManagedIssuanceSource.OpenAsync(scope, row, token);
        if (!Authorization)
        {
            email = source.Email(row);
            ManagedEmailClaims.Attach(principal!, email);
            RequirePrincipal();
        }
        initial = source.Snapshot(row);
        await SaveAsync(initial, token);
        ProtocolStorageAmbient.Current!.Request.RestrictPublication(DateTimeOffset.FromUnixTimeSeconds(Math.Min(row.ExpiresAt, registration.ExpiresAt)));
    }

    /// <summary>Reserves exactly the native evaluated plan before any authorization or token entry can be created.</summary>
    internal async ValueTask ReserveAsync(CancellationToken token)
    {
        if (row is null || source is null || proof is not null || !Plan())
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        RequirePrincipal();
        cutoff = Math.Max(0, ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt) - (long)Math.Ceiling(context.Options.TokenValidationParameters.ClockSkew.TotalSeconds));
        budget = await ManagedNativeBudget.ReadAsync(scope, cutoff, token);
        if (budget.Tokens < 0 || budget.Tokens > ManagedLoginRules.TokenLimit - (Authorization ? 1 : ExchangeTokens) ||
            budget.Authorizations < 0 || budget.Authorizations > ManagedLoginRules.AuthorizationLimit - (Authorization ? 1 : 0) ||
            budget.Requests > ManagedLoginRules.RequestLimit || budget.LiveRequests > ManagedLoginRules.LiveLimit)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        proof = new(scope, context, Authorization ? context.Request.RedirectUri! : context.Principal!.GetClaim(OpenIddictConstants.Claims.Private.RedirectUri)!, RequirePrepared);
    }

    /// <summary>Requires exactly code/query or code-exchange output, with no cached request, refresh, device or additional native credential.</summary>
    private bool Plan()
    {
        if (context.GenerateDeviceCode || context.IncludeDeviceCode || context.GenerateUserCode || context.IncludeUserCode ||
            context.GenerateRefreshToken || context.IncludeRefreshToken || context.GenerateIssuedToken || context.IncludeIssuedToken ||
            context.GenerateRequestToken || context.IncludeRequestToken)
        {
            return false;
        }
        var flags = (context.GenerateAuthorizationCode, context.IncludeAuthorizationCode, context.GenerateAccessToken,
            context.IncludeAccessToken, context.GenerateIdentityToken, context.IncludeIdentityToken);
        return Authorization ? flags == (true, true, false, false, false, false) && context.Principal!.GetAuthorizationId() is null :
            flags == (false, false, true, true, true, true) && context.Request.IsAuthorizationCodeGrantType() && context.Principal!.GetAuthorizationId() == row!.AuthorizationId;
    }

    /// <summary>Bounds genuine prepared principals to the original human, native-input and credential deadlines before cryptography.</summary>
    internal void Bound()
    {
        RequirePrincipal();
        if (proof is null || row is null || registration is null || binding is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var maximum = Math.Min(Math.Min(row.ExpiresAt, registration.ExpiresAt), binding.Expiration.ToUnixTimeSeconds());
        foreach (var current in Prepared())
        {
            if (current?.GetExpirationDate() is not { } expires || current.GetCreationDate() is not { } created || created >= expires)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            current.SetExpirationDate(DateTimeOffset.FromUnixTimeSeconds(Math.Min(expires.ToUnixTimeSeconds(), maximum)));
        }
    }

    /// <summary>Verifies original source, actual native creation and durable consumption before recording only the intended final row/audit delta.</summary>
    internal async ValueTask CommitAsync(CancellationToken token)
    {
        if (transaction is null || row is null || source is null || initial is null || proof is null || consumption is null || registration is null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await VerifyAsync(initial, token);
        if (!await consumption.VerifyAsync(scope, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var audit = await scope.Owner.RunAsync(_ => ValueTask.FromResult(Stage()), token, write: true);
        var final = source.Snapshot(row, audit);
        await SaveAsync(final, token);
        await VerifyAsync(final, token);
        if (!await consumption.VerifyAsync(scope, token, originalRow: false))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await transaction.CommitAsync(token);
    }

    /// <summary>Creates one finite credential-free audit and advances consent only with the exact native code and authorization.</summary>
    private SecurityAuditIntent Stage()
    {
        row!.ObservedAt = ManagedLoginRules.Observe(scope.Owner.Clock, row.ObservedAt);
        if (Authorization)
        {
            row.State = ManagedLoginState.Consumed;
            row.Revision = checked(row.Revision + 1);
            row.CodeId = context.AuthorizationCodePrincipal!.GetTokenId();
            row.AuthorizationId = context.AuthorizationCodePrincipal!.GetAuthorizationId();
        }
        var prepared = Prepared().Select(value => value!).ToArray();
        row.NativeDependencyCreatedAt = Math.Max(row.NativeDependencyCreatedAt ?? row.CreatedAt, prepared.Max(value => value.GetCreationDate()!.Value.ToUnixTimeSeconds()));
        row.NativeDependencyExpiresAt = Math.Max(row.NativeDependencyExpiresAt ?? row.ExpiresAt, prepared.Max(value => value.GetExpirationDate()!.Value.ToUnixTimeSeconds()));
        row.RetainUntil = Math.Max(row.RetainUntil, ManagedLoginRules.RetainUntil(row.NativeDependencyCreatedAt.Value, row.NativeDependencyExpiresAt.Value, context.Options.TokenValidationParameters.ClockSkew));
        var audit = new SecurityAuditIntent
        {
            Event = Authorization ? CodeEvent : TokenEvent,
            ActorType = Account,
            ActorId = row.UserId,
            TargetType = Application,
            TargetId = row.ApplicationId,
            Outcome = Accepted,
            CorrelationId = row.CorrelationId,
            OccurredAt = row.ObservedAt
        };
        scope.Owner.Database.Add(audit);
        return audit;
    }

    /// <summary>Checks persisted snapshots, exact native row deltas and unchanged policy immediately before the single issuance commit.</summary>
    private async ValueTask VerifyAsync(ManagedHumanSnapshot snapshot, CancellationToken token)
    {
        RequirePrincipal();
        await scope.Owner.RunAsync(current => new ValueTask(snapshot.VerifyAsync((PlatformDbContext)scope.Owner.Database, scope.Owner.Clock, current)), token);
        await ManagedScopeBinding.RequireAsync(scope, row!, registration!.ClientId, token);
        var actual = await ManagedNativeBudget.ReadAsync(scope, cutoff, token);
        if (actual.Tokens != budget.Tokens + (Authorization ? 1 : ExchangeTokens) || actual.Authorizations != budget.Authorizations + (Authorization ? 1 : 0) ||
            actual.Requests != budget.Requests || actual.LiveRequests != budget.LiveRequests || !await proof!.VerifyAsync(registration!.ApplicationId, token) ||
            !registration.Matches(await ManagedRegistration.ReadAsync(scope, registration.ClientId, token)) ||
            ManagedLoginRules.Observe(scope.Owner.Clock, Math.Max(row!.ObservedAt, registration.ObservedAt)) >= Math.Min(row.ExpiresAt, registration.ExpiresAt) ||
            Prepared().Any(value => value?.GetExpirationDate() is not { } expiration || expiration <= scope.Owner.Clock.GetUtcNow() || expiration.ToUnixTimeSeconds() > Math.Min(row.ExpiresAt, registration.ExpiresAt)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Persists already-frozen intended state and immediately rejects tracked or independently stored callback mutations.</summary>
    private ValueTask SaveAsync(ManagedHumanSnapshot snapshot, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        await scope.Owner.Database.SaveChangesAsync(current);
        await snapshot.VerifyAsync((PlatformDbContext)scope.Owner.Database, scope.Owner.Clock, current);
    }, token, write: true);

    /// <summary>Rejects principal replacement, administrator provenance, unexpected native private claims and altered source identity.</summary>
    private void RequirePrincipal()
    {
        if (row is null || principal is null || !ReferenceEquals(principal, context.Principal) ||
            !ManagedPrincipal.Matches(principal, row, context.ClientId!, selector!, email: email) ||
            !Authorization && (ManagedConsumption.Original(context) is not { } original || !ManagedPrincipal.Restored(principal, original)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    private bool Authorization => context.EndpointType == OpenIddictServerEndpointType.Authorization;

    /// <summary>Checks the native prepared clone before signing and after formatting against the original managed source and deadlines.</summary>
    private void RequirePrepared(ClaimsPrincipal value, string type)
    {
        RequirePrincipal();
        var maximum = Math.Min(Math.Min(row!.ExpiresAt, registration!.ExpiresAt), binding!.Expiration.ToUnixTimeSeconds());
        if (!ManagedPreparedPrincipal.Matches(context, value, type, row, selector!, maximum, email))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Returns only the native prepared principals selected by the exact reserved plan.</summary>
    private ClaimsPrincipal?[] Prepared() => Authorization ? [context.AuthorizationCodePrincipal] : [context.AccessTokenPrincipal, context.IdentityTokenPrincipal];

    /// <summary>Retains actual owning transaction cleanup through commit acknowledgement and disposal failure.</summary>
    public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
}
