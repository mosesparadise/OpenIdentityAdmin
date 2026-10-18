using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Starts live issuance authority after native early redemption and internal-claim restoration.</summary>
internal sealed class OpenAdministratorIssuance : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Uses only the fresh child state installed by explicit coordinator routing.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context) =>
        AdministratorIssuance.Find(context.Transaction)?.OpenAsync(context.CancellationToken) ?? ValueTask.CompletedTask;
}

/// <summary>Reserves bounded native persistence after the installed evaluator selects actual generated artifacts.</summary>
internal sealed class ReserveAdministratorIssuance : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Retains the same write transaction through every subsequent native create and final commit check.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context) =>
        AdministratorIssuance.Find(context.Transaction)?.ReserveAsync(context.CancellationToken) ?? ValueTask.CompletedTask;
}

/// <summary>Applies fixed absolute expiration bounds to prepared native principals before generation starts.</summary>
internal sealed class BoundAdministratorIssuance : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    /// <summary>Preserves native claims and crypto while preventing preparation time from extending the captured lifetime ceiling.</summary>
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        AdministratorIssuance.Find(context.Transaction)?.BoundExpiration();
        return ValueTask.CompletedTask;
    }
}
