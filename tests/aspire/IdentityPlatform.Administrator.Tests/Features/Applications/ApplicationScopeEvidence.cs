using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Catalogue;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Independently observes actual permissions and every native field outside the explicit assignment mutation.</summary>
internal static class ApplicationScopeEvidence
{
    internal const string Email = "email";
    internal const string Orders = "orders.read";
    private const string Display = "Assignment test";
    private const string Consent = "Share the selected data";
    private const string Owner = "Operations";
    private const string Resource = "orders-api";
    private const string IdentityKind = "identity";
    private const string ApiKind = "api";
    private const string None = "none";
    private const string PermissionField = "Permissions";
    private const string StampField = "ConcurrencyToken";

    private const int Created = 201;

    /// <summary>Creates eligible identity/API policies through the real audited catalogue coordinator rather than a parallel assignment store.</summary>
    internal static async Task<CatalogueScope[]> SeedAsync(ApplicationMutationFixture test)
    {
        var audience = await CatalogueMutationTests.ExecuteAsync(test, CatalogueIntent.Create(new AudienceInput(Resource, Display, Owner, true))!);
        Assert.Equal(Created, audience.Status);
        var email = await CatalogueMutationTests.ExecuteAsync(test, CatalogueIntent.Create(new ScopeInput(Email, Display, IdentityKind, true, Consent, null, Email))!);
        var orders = await CatalogueMutationTests.ExecuteAsync(test, CatalogueIntent.Create(new ScopeInput(Orders, Display, ApiKind, true, Consent, audience.Audience!.Id, None))!);
        Assert.Equal(Created, email.Status);
        Assert.Equal(Created, orders.Status);
        return [email.Scope!, orders.Scope!];
    }

    /// <summary>Observes a fresh native row; a separate digest permits only permission/stamp changes without weakening existing editor preservation oracles.</summary>
    internal static async Task<(string[] Permissions, string Stable)> ReadAsync(ApplicationMutationFixture test, string id)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == id, test.Token);
        var stable = database.Entry(native).Properties.Where(value => value.Metadata.Name is not (PermissionField or StampField))
            .OrderBy(value => value.Metadata.Name, StringComparer.Ordinal).ToDictionary(value => value.Metadata.Name, value => value.CurrentValue);
        return (JsonSerializer.Deserialize<string[]>(native.Permissions!)!.Order(StringComparer.Ordinal).ToArray(), ApplicationUpdateSnapshot.Hash(stable));
    }
}
