using System.Text.Json.Nodes;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises source-session, live role grants, native token expiry and rollback-only ownership for contract projection.</summary>
internal static class DocumentReadAuthority
{
    private const string Read = "applications.read";
    private const string Paths = "paths";
    private const string Route = "/api/v1/applications";
    private const string Document = """{"paths":{"/api/v1/applications":{"get":{"x-required-permission":"applications.read"}}}}""";

    /// <summary>Requires fresh authorization for each reader call after a role change and earlier native token expiry.</summary>
    internal static async Task RequireAsync(ApplicationMutationFixture test)
    {
        Assert.NotNull((await ProjectAsync(test))?[Paths]?[Route]);
        await using (var scope = test.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Read).ExecuteDeleteAsync(test.Token) > 0);
        }
        var reduced = await ProjectAsync(test);
        Assert.NotNull(reduced);
        Assert.Empty(reduced[Paths]!.AsObject());
        await using (var scope = test.Scope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            var entry = (await tokens.FindByIdAsync(test.Identity.TokenId, test.Token))!;
            var descriptor = new OpenIddictTokenDescriptor();
            await tokens.PopulateAsync(descriptor, entry, test.Token);
            descriptor.ExpirationDate = test.Clock.Now.UtcDateTime;
            await tokens.UpdateAsync(entry, descriptor, test.Token);
        }
        Assert.True(test.Identity.ExpiresAt > test.Clock.Now);
        Assert.Null(await ProjectAsync(test));
    }

    /// <summary>Uses the actual protected API reader transaction and rejects all calls made after that ownership ends.</summary>
    private static async Task<JsonObject?> ProjectAsync(ApplicationMutationFixture test)
    {
        var document = JsonNode.Parse(Document)!.AsObject();
        await using var scope = test.Api.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        var callback = services.GetRequiredService<ApiResourceProfile>().Callback;
        var transaction = await CeremonyTransaction.OpenApiReadAsync(database, services.GetRequiredService<NativeOperation>(), test.Identity.Subject, test.Token);
        JsonObject? result;
        await using (transaction)
        {
            await services.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, test.Identity.Subject);
            result = await ApiCurrentAccess.ProjectAsync(services, database, transaction, test.Identity, callback, test.Clock, document);
        }
        await Assert.ThrowsAsync<FactorUnavailableException>(() => ApiCurrentAccess.ProjectAsync(services, database, transaction, test.Identity, callback, test.Clock, document));
        return result;
    }
}
