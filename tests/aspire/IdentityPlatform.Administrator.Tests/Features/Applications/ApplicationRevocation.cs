using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Names independent committed authority defects in synthetic integration fixtures.</summary>
public enum ApplicationRevocation
{
    Token, Authorization, SourceSession, Account, Profile, Factor, SecurityStamp
}

/// <summary>Commits one deliberately isolated authority change after both API roots have observed the original valid state.</summary>
internal static class ApplicationRevocationWriter
{
    private const string ChangedStamp = "changed-application-test-stamp";

    /// <summary>Uses real provider writes and awaits their implicit commit; none of these test-only operations is a product administrative command.</summary>
    internal static async Task RevokeAsync(PlatformDbContext database, string subject, string source, ApplicationRevocation defect)
    {
        var token = TestContext.Current.CancellationToken;
        var affected = defect switch
        {
            ApplicationRevocation.Token => await database.Set<OpenIddictEntityFrameworkCoreToken>()
                .Where(row => row.Subject == subject && row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, OpenIddictConstants.Statuses.Revoked), token),
            ApplicationRevocation.Authorization => await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(row => row.Subject == subject)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, OpenIddictConstants.Statuses.Revoked), token),
            ApplicationRevocation.SourceSession => await database.Set<AdministratorSession>().Where(row => row.UserId == subject && row.PublicId == source)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), token),
            ApplicationRevocation.Account => await database.Users.Where(row => row.Id == subject)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), token),
            ApplicationRevocation.Profile => await database.Set<AdministratorProtocolProfile>()
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, false), token),
            ApplicationRevocation.Factor => await database.Set<AdministratorEnrollment>().Where(row => row.UserId == subject)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.FactorGeneration, row => row.FactorGeneration + 1), token),
            ApplicationRevocation.SecurityStamp => await database.Users.Where(row => row.Id == subject)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.SecurityStamp, ChangedStamp), token),
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };
        Assert.Equal(1, affected);
    }
}
