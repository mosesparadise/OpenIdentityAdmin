using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Rejects suppressed atomic evidence and unmanaged native scope adoption on both actual provider implementations.</summary>
[Collection("administrator-bootstrap")]
public sealed class CatalogueAtomicityTests(BootstrapFixture fixture)
{
    private const string Name = "email";
    private const string Display = "Email";
    private const string Identity = "identity";
    private const string Consent = "Share email";
    private const string ExistingDisplay = "Existing unmanaged scope";

    /// <summary>A suppressed audit after actual native/policy writes rolls back every new row and cannot release a successful projection.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SuppressedCatalogueAudit_When_NativeScopeAndPolicyWereWritten_Then_NoPartialStateOrSuccessEscapes(string provider)
    {
        var fault = new CatalogueAuditSuppression();
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var intent = CatalogueIntent.Create(new ScopeInput(Name, Display, Identity, true, Consent, null, Name))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogueMutationTests.ExecuteAsync(test, intent));
        Assert.True(fault.ObservedNativeAndPolicy);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().AnyAsync(row => row.Name == Name, test.Token));
        Assert.Empty(await database.Set<ScopePolicy>().AsNoTracking().ToArrayAsync(test.Token));
        Assert.False(await database.Set<SecurityAuditIntent>().AsNoTracking().AnyAsync(row => row.TargetType == CatalogueAssertions.ScopeTarget, test.Token));
    }

    /// <summary>A matching unmanaged native name remains untouched and receives no policy or success audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExistingUnmanagedNativeScope_When_CatalogueCreationRequested_Then_DuplicateCannotAdoptOrRewriteIt(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await using var setup = test.Scope();
        var manager = setup.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = Name, DisplayName = ExistingDisplay }, test.Token);
        var id = await manager.GetIdAsync(native, test.Token);
        var intent = CatalogueIntent.Create(new ScopeInput(Name, Display, Identity, true, Consent, null, Name))!;
        Assert.Equal(409, (await CatalogueMutationTests.ExecuteAsync(test, intent)).Status);
        await using var observer = test.Scope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actual = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().SingleAsync(row => row.Id == id, test.Token);
        Assert.Equal(ExistingDisplay, actual.DisplayName);
        Assert.Empty(await database.Set<ScopePolicy>().AsNoTracking().ToArrayAsync(test.Token));
        Assert.False(await database.Set<SecurityAuditIntent>().AsNoTracking().AnyAsync(row => row.TargetType == CatalogueAssertions.ScopeTarget, test.Token));
    }
}
