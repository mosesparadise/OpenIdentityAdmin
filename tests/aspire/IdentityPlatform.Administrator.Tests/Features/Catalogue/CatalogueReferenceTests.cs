using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Observes real native reference projections against ordinal, escaped, malformed and overflowing retained data on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueReferenceTests(BootstrapFixture fixture)
{
    private const string Name = "email";
    private const string Resource = "orders-api";
    private const string Client = "catalogue-reference-test";
    private const string Other = "other-scope";
    private const string PermissionCase = "[\"scp:Email\"]";
    private const string PermissionPrefix = "[\"scp:emails\"]";
    private const string EscapedPermission = "[\"scp:\\u0065mail\"]";
    private const string EscapedOther = "[\"scp:\\u006fther\"]";
    private const string InvalidArray = "[\"scp:email\",null]";
    private const string MalformedArray = "[\"scp:email\"";
    private const int MaximumJson = 32_768;
    private const int MaximumCandidates = 128;
    private const string EmptyArray = "[]";
    private const string EscapedResource = "[\"orders-\\u0061pi\"]";
    private const int OversizedJson = 32_769;
    private const int OverflowCandidates = 129;

    /// <summary>Broad SQL collation/prefix candidates cannot count as exact permissions, while alternate valid JSON encodings remain visible.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativePermissionVariants_When_ReferencesRead_Then_DecodedOrdinalMembershipControlsResult(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var setup = test.Scope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var manager = setup.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var native = await manager.CreateAsync(new OpenIddictApplicationDescriptor { ClientId = Client, ClientType = OpenIddictConstants.ClientTypes.Public }, test.Token);
        var id = await manager.GetIdAsync(native, test.Token);
        foreach (var json in new[] { PermissionCase, PermissionPrefix, EscapedPermission, InvalidArray, MalformedArray })
        {
            await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Permissions, json), test.Token);
            if (json is InvalidArray or MalformedArray)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(test, audience: false));
            }
            else
            {
                Assert.Equal(json == EscapedPermission, await ReadAsync(test, audience: false));
            }
        }
        await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Permissions, EmptyArray), test.Token);
        var authorizations = setup.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var retained = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Status = OpenIddictConstants.Statuses.Revoked,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        };
        retained.Scopes.Add(Name);
        await authorizations.CreateAsync(retained, test.Token);
        Assert.True(await ReadAsync(test, audience: false));
    }

    /// <summary>Unmanaged native resource mappings block audience deletion even when their JSON uses escaped spelling.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_UnmanagedEscapedResource_When_AudienceReferenceRead_Then_CurrentNativeDependencyIsVisible(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        Assert.False(await ReadAsync(test, audience: true));
        await using var setup = test.Scope();
        var manager = setup.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = Other }, test.Token);
        var id = await manager.GetIdAsync(native, test.Token);
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.Set<OpenIddictEntityFrameworkCoreScope>().Where(row => row.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Resources, EscapedResource), test.Token);
        Assert.True(await ReadAsync(test, audience: true));
    }

    /// <summary>Overlong or too many plausible native records cannot be silently truncated into a safe-to-delete answer.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_OverflowingNativeCandidates_When_ReferenceDecisionRequested_Then_UnavailableReplacesUnreferenced(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var setup = test.Scope();
        var database = setup.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = Client, Permissions = EscapedOther.PadRight(MaximumJson) };
        database.Add(row);
        await database.SaveChangesAsync(test.Token);
        Assert.False(await ReadAsync(test, audience: false));
        row.Permissions = EscapedOther.PadRight(OversizedJson);
        await database.SaveChangesAsync(test.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(test, audience: false));
        row.Permissions = EscapedOther;
        for (var index = 1; index < MaximumCandidates; index++)
        {
            database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = Guid.NewGuid().ToString(), Permissions = EscapedOther });
        }
        await database.SaveChangesAsync(test.Token);
        Assert.False(await ReadAsync(test, audience: false));
        database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = Guid.NewGuid().ToString(), Permissions = EscapedOther });
        await database.SaveChangesAsync(test.Token);
        Assert.Equal(OverflowCandidates, await database.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync(row => row.Permissions == EscapedOther, test.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(test, audience: false));
    }

    /// <summary>Uses the existing owned store boundary and governance serialization; this read-only test helper issues no administrator capability.</summary>
    private static async Task<bool> ReadAsync(ApplicationMutationFixture test, bool audience)
    {
        var request = new ProtocolStorageRequest(test.Clock, test.Token);
        try
        {
            return await request.RunAsync(test.Api.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await ManagedRegistration.LockAsync(scope, token);
                return audience ? await CatalogueNativeReferences.AudienceAsync(scope.Services, Resource, token) : await CatalogueNativeReferences.ScopeAsync(scope.Services, Name, token);
            });
        }
        finally { await request.DrainAsync(); }
    }
}
