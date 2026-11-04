using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves update rollback preserves existing credentials and immutable command/resource bindings.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationMutationRollbackTests(BootstrapFixture fixture)
{
    private const string Display = "Changed safely";
    private const string Redirect = "https://managed.example.test/changed";
    private const string Padding = " ";

    /// <summary>Both a replacement audit outage and tracked credential drift during PUT roll back the original native hash and all policy fields.</summary>
    [Theory]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExistingRegistration_When_UpdatePersistenceIsFaulted_Then_OriginalSecretLifecycleVersionAndAuditRemain(string provider, bool replacement)
    {
        var fault = new ApplicationWriteFault(replacement ? ApplicationWriteDefect.AuditFailure : ApplicationWriteDefect.TrackedCredentialDrift);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        var before = await ReadAsync(test);
        fault.Armed = true;
        var command = replacement ? test.Replace(created) : ApplicationMutationCommand.Edit(created.Application!.Id, created.Version!, new(Display, [Redirect]))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.ExecuteAsync(command));
        Assert.True(fault.Fired);
        var after = await ReadAsync(test);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.CredentialRevision, after.CredentialRevision);
        Assert.Equal(before.CredentialIssuedAt, after.CredentialIssuedAt);
        Assert.Equal(before.CredentialExpiresAt, after.CredentialExpiresAt);
        Assert.Equal(before.ObservedAt, after.ObservedAt);
        Assert.True(await test.AcceptsAsync(created.Application!.Id, created.Credential!.ClientSecret));
        Assert.Equal(1, await test.AuditsAsync());
    }

    /// <summary>Provider padding or case aliases cannot target another exact opaque identifier even with that original record's valid version.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_IdentifierAliases_When_UpdateCarriesOriginalVersion_Then_ExactResourceBindingDenies(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        var id = created.Application!.Id;
        foreach (var alias in new[] { id + Padding, id.ToUpperInvariant() }.Where(value => value != id))
        {
            var command = ApplicationMutationCommand.Edit(alias, created.Version!, new(Display, [Redirect]))!;
            Assert.Equal(404, (await test.ExecuteAsync(command)).Status);
        }
        Assert.Equal(1, (await ReadAsync(test)).Revision);
        Assert.Equal(1, await test.AuditsAsync());
    }

    /// <summary>Uses a fresh detached query for every expected and actual lifecycle observation.</summary>
    private static async Task<ApplicationPolicy> ReadAsync(ApplicationMutationFixture test)
    {
        await using var scope = test.Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationPolicy>().AsNoTracking().SingleAsync(test.Token);
    }
}
