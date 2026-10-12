using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Records anonymous password rejection without accepting caller-controlled actor, target or outcome data.</summary>
internal sealed class JourneyDenial(IServiceScopeFactory scopes, TimeProvider clock)
{
    /// <summary>Uses a separate clean bounded context; audit failure propagates and cannot produce authentication authority.</summary>
    internal async Task WriteAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        await using var scope = scopes.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var connection = new CeremonyConnection(database);
        await connection.OpenAsync(deadline.Token);
        await using var transaction = await database.Database.BeginTransactionAsync(deadline.Token);
        const string Event = "administrator-password";
        const string Anonymous = "anonymous";
        const string Target = "administrator-account";
        const string Denied = "denied";
        database.Add(new SecurityAuditIntent
        {
            Event = Event,
            ActorType = Anonymous,
            TargetType = Target,
            Outcome = Denied,
            OccurredAt = clock.GetUtcNow().ToUnixTimeSeconds()
        });
        await database.SaveChangesAsync(deadline.Token);
        await transaction.CommitAsync(deadline.Token);
    }
}
