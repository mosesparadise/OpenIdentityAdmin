using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises actual native manager/policy/audit atomicity under current API authority on both providers; HTTP/CSRF integration remains separate.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationMutationTests(BootstrapFixture fixture)
{
    private const string Edited = "Edited managed application";
    private const string Write = "applications.write";
    private const string Credentials = "applications.credentials";
    private const string ReplacementOnly = "replacement-only";
    private const int MfaSeconds = 300;
    private const int FinalRevision = 3;
    private const int EditRevision = 2;
    private const int CreatedStatus = 201;
    private const int UpdatedStatus = 200;
    private const string ChangedRedirect = "https://managed.example.test/new-callback";

    /// <summary>Creation, versioned edit and explicit replacement preserve exact fields, native hash behavior, lifecycle and one audit per successful operation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentWriteAuthority_When_CreateEditAndReplacementCommit_Then_NativeSecretsVersionsAndAuditsAgree(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        Assert.NotNull(created.Credential);
        Assert.True(await test.AcceptsAsync(created.Application!.Id, created.Credential.ClientSecret));
        var edited = await test.ExecuteAsync(ApplicationMutationCommand.Edit(created.Application.Id, created.Version!, new(Edited, [ChangedRedirect]))!);
        Assert.Equal(200, edited.Status);
        Assert.Null(edited.Credential);
        await ApplicationMutationAssertions.RequireAsync(test, edited, Edited, ChangedRedirect);
        Assert.Equal(Edited, edited.Application!.DisplayName);
        Assert.Equal(created.Application.Credential, edited.Application.Credential);
        Assert.Equal(ApplicationVersion.Create(created.Application.Id, EditRevision), edited.Version);
        Assert.True(await test.AcceptsAsync(created.Application.Id, created.Credential.ClientSecret));
        var replaced = await test.ExecuteAsync(test.Replace(edited));
        Assert.Equal(200, replaced.Status);
        Assert.NotNull(replaced.Credential);
        Assert.Equal(ApplicationVersion.Create(created.Application.Id, FinalRevision), replaced.Version);
        Assert.False(await test.AcceptsAsync(created.Application.Id, created.Credential.ClientSecret));
        Assert.True(await test.AcceptsAsync(created.Application.Id, replaced.Credential.ClientSecret));
        Assert.Equal(FinalRevision, await test.AuditsAsync());
    }

    /// <summary>Same-resource races serialize under actual provider locks and only the winning duplicate/versioned write receives an audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConcurrentCommands_When_ClientOrVersionMatches_Then_OnlyOneMutationAndAuditCommit(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var creates = await Task.WhenAll(test.ExecuteAsync(test.Create()), test.ExecuteAsync(test.Create()));
        Assert.Equal([201, 409], creates.Select(value => value.Status).Order().ToArray());
        var created = Assert.Single(creates, value => value.Status == CreatedStatus);
        var command = ApplicationMutationCommand.Edit(created.Application!.Id, created.Version!, new(Edited, [ApplicationMutationFixture.Redirect]))!;
        var edits = await Task.WhenAll(test.ExecuteAsync(command), test.ExecuteAsync(command));
        Assert.Equal([200, 412], edits.Select(value => value.Status).Order().ToArray());
        var edited = Assert.Single(edits, value => value.Status == UpdatedStatus);
        var replacement = test.Replace(edited);
        var replacements = await Task.WhenAll(test.ExecuteAsync(replacement), test.ExecuteAsync(replacement));
        Assert.Equal([200, 412], replacements.Select(value => value.Status).Order().ToArray());
        Assert.Equal(FinalRevision, await test.AuditsAsync());
    }

    /// <summary>Read access does not authorize writes; removing the credential grant denies replacement while ordinary edits remain permitted.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentPersistedGrants_When_MutationIsRequested_Then_OnlyItsSpecificPermissionAuthorizes(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        await RemoveAsync(test, Credentials);
        Assert.Equal(403, (await test.ExecuteAsync(test.Replace(created))).Status);
        var edited = await test.ExecuteAsync(ApplicationMutationCommand.Edit(created.Application!.Id, created.Version!, new(Edited, [ApplicationMutationFixture.Redirect]))!);
        Assert.Equal(200, edited.Status);
        await RemoveAsync(test, Write);
        Assert.Equal(403, (await test.ExecuteAsync(test.Create(ReplacementOnly))).Status);
        Assert.Equal(2, await test.AuditsAsync());
    }

    /// <summary>Equality at the persisted MFA deadline denies replacement while the otherwise current session can still edit presentation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExpiredRecentMfa_When_ReplacementIsRequested_Then_CredentialIsPreservedAndOrdinaryEditStillWorks(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var verified = await database.Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds);
        Assert.Equal(403, (await test.ExecuteAsync(test.Replace(created))).Status);
        var edited = await test.ExecuteAsync(ApplicationMutationCommand.Edit(created.Application!.Id, created.Version!, new(Edited, [ApplicationMutationFixture.Redirect]))!);
        Assert.Equal(200, edited.Status);
        Assert.True(await test.AcceptsAsync(created.Application.Id, created.Credential!.ClientSecret));
        Assert.Equal(2, await test.AuditsAsync());
    }

    /// <summary>Deletes only the selected persisted grant in the isolated fixture; current authority must observe its absence.</summary>
    private static async Task RemoveAsync(ApplicationMutationFixture test, string permission)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == permission).ExecuteDeleteAsync(test.Token) > 0);
    }
}
