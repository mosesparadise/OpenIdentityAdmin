using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Discovers untrusted candidate locks inside the owned phase; authoritative state is loaded only after the entire plan is acquired.</summary>
internal static class ManagedHumanCandidates
{
    /// <summary>Declares governance/admission plus existing browser/account locks without allocating state or checking credentials.</summary>
    internal static ValueTask<CeremonyLockPlan> ReadAsync(ProtocolStorageScope scope, ManagedHumanRequest request,
        ManagedHumanAction action, string browser, string? username, CancellationToken token) => scope.Owner.RunAsync(async current =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            var digest = ManagedLoginRules.Digest(request.Selector);
            var row = await database.Set<ManagedLoginInteraction>().AsNoTracking().SingleOrDefaultAsync(value => value.SelectorDigest == digest, current);
            string? user = row?.UserId;
            if (action == ManagedHumanAction.Password)
            {
                var name = scope.Services.GetRequiredService<ILookupNormalizer>().NormalizeName(username);
                user = await database.Users.AsNoTracking().Where(value => value.NormalizedUserName == name).Select(value => value.Id).SingleOrDefaultAsync(current);
                if (user is not null && !await database.Set<AdministratorEnrollment>().AnyAsync(value => value.UserId == user, current))
                {
                    user = null;
                }
            }
            var budget = CeremonySecrets.Digest(browser);
            var existing = await database.Set<AdministratorBrowserBudget>().AnyAsync(value => value.Id == budget, current);
            return new CeremonyLockPlan(true, true, existing ? [budget] : [], user is null ? [] : [user]);
        }, token);
}
