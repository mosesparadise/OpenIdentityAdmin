using System.Globalization;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves clone isolation and current authority through actual owned native writes on each relational provider.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationCloneTests(BootstrapFixture fixture)
{
    private const string ApplicationKey = "ApplicationId";
    private const string AuthorizationKey = "AuthorizationId";
    private const string Email = "email";
    private const string Orders = "orders.read";
    private const string EmailPermission = "scp:email";
    private const string OrdersPermission = "scp:orders.read";
    private const string AnotherClient = "another-clone";
    private const string ChangedDisplay = "Changed source";
    private const string ValidStatus = "valid";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const int InitialScopeRevision = 2;
    private const int MfaSeconds = 300;
    private const int GrantMinutes = 10;
    private const string Destination = "independent-clone";
    private const string Display = "Independent clone";
    private const string CloneEvent = "ApplicationCloned";
    private const string DisabledWitness = "application-clone-disabled-bypass";

    /// <summary>A fresh clone has its own ID/hash/lifecycle and exact source provenance, without changing the source.</summary>
    [Trait("CriticalDecision", "application-clone-disabled")]
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentManagedSource_When_Cloned_Then_NewDisabledIdentityAndSecretLeaveSourceUnchanged(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        await ApplicationScopeEvidence.SeedAsync(test);
        var created = await test.ExecuteAsync(test.Create());
        var assigned = await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(created.Application!.Id, created.Version!,
            new([Email, Orders]))!);
        Assert.Equal(200, assigned.Status);
        var source = assigned with
        {
            Credential = created.Credential
        };
        await SeedSourceGrantsAsync(test, source.Application!.Id);
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id);
        var grants = await GrantsAsync(test, source.Application.Id);
        var clone = await test.ExecuteAsync(Command(test, source));
        Assert.Equal(201, clone.Status);
        Assert.NotNull(clone.Application);
        Assert.NotNull(clone.Credential);
        Assert.NotEqual(source.Application!.Id, clone.Application.Id);
        Assert.False(clone.Application.Enabled, DisabledWitness);
        Assert.Equal(Destination, clone.Application.ClientId);
        Assert.Equal(Display, clone.Application.DisplayName);
        Assert.Equal(source.Application.RedirectUris, clone.Application.RedirectUris);
        Assert.Equal(ApplicationVersion.Create(clone.Application.Id, 1), clone.Version);
        await RequireCredentialsAsync(test, source, clone);
        Assert.Equal(grants, await GrantsAsync(test, source.Application.Id));
        await RequireIsolationAsync(test, source, clone);
        var after = await ApplicationUpdateSnapshot.ReadAsync(test, source.Application.Id);
        Assert.Equal(before.Audits.Length + 1, after.Audits.Length);
        Assert.Equal(before.NativeDigest, after.NativeDigest);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Policy), ApplicationUpdateSnapshot.Hash(after.Policy));
        Assert.All(before.Audits, old => Assert.Equal(ApplicationUpdateSnapshot.Hash(old), ApplicationUpdateSnapshot.Hash(Assert.Single(after.Audits, row => row.Id == old.Id))));
        var expected = await ApplicationScopeEvidence.ReadAsync(test, source.Application.Id);
        var copied = await ApplicationScopeEvidence.ReadAsync(test, clone.Application.Id);
        Assert.Equal(expected.Permissions, copied.Permissions);
        Assert.Contains(EmailPermission, copied.Permissions);
        Assert.Contains(OrdersPermission, copied.Permissions);
    }

    /// <summary>Duplicate destination and stale source cannot silently retry or mutate the established source/destination.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_UsedDestinationOrChangedSource_When_CloneSubmitted_Then_NoAdditionalApplicationOrAuditCommits(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var source = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, (await test.ExecuteAsync(Command(test, source))).Status);
        Assert.Equal(409, (await test.ExecuteAsync(Command(test, source))).Status);
        var edited = await test.ExecuteAsync(ApplicationMutationCommand.Edit(source.Application!.Id, source.Version!,
            new(ChangedDisplay, [ApplicationMutationFixture.Redirect]))!);
        Assert.Equal(200, edited.Status);
        Assert.Equal(412, (await test.ExecuteAsync(Command(test, source, AnotherClient))).Status);
        Assert.Equal(3, await test.AuditsAsync());
        await using var scope = test.Scope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(AnotherClient, test.Token));
    }

    /// <summary>Copying assigned configuration retains fresh-MFA authority even when the source has no additional scopes.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExpiredFreshMfa_When_CloneSubmitted_Then_SourceAndAuditRemainUnchanged(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var source = await test.ExecuteAsync(test.Create());
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var verified = await database.Set<AdministratorSession>().Select(row => row.FactorVerifiedAt).SingleAsync(test.Token);
        test.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(verified + MfaSeconds);
        Assert.Equal(403, (await test.ExecuteAsync(Command(test, source))).Status);
        Assert.Equal(1, await test.AuditsAsync());
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(Destination, test.Token));
    }

    /// <summary>Requires each native hash to accept only its own generated credential, without logging either secret.</summary>
    private static async Task RequireCredentialsAsync(ApplicationMutationFixture test, ApplicationMutationResult source, ApplicationMutationResult clone)
    {
        Assert.True(await test.AcceptsAsync(clone.Application!.Id, clone.Credential!.ClientSecret));
        Assert.False(await test.AcceptsAsync(clone.Application!.Id, source.Credential!.ClientSecret));
        Assert.True(await test.AcceptsAsync(source.Application!.Id, source.Credential!.ClientSecret));
        Assert.False(await test.AcceptsAsync(source.Application!.Id, clone.Credential!.ClientSecret));
    }

    /// <summary>Checks persisted source/destination metadata and provenance independently of returned DTOs.</summary>
    private static async Task RequireIsolationAsync(ApplicationMutationFixture test, ApplicationMutationResult source, ApplicationMutationResult clone)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var original = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == source.Application!.Id, test.Token);
        Assert.Equal(InitialScopeRevision, original.Revision);
        Assert.True(original.Enabled);
        var destination = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == clone.Application!.Id, test.Token);
        Assert.False(destination.Enabled, DisabledWitness);
        Assert.Equal(1, destination.CredentialRevision);
        Assert.Equal(test.Clock.Now.ToUnixTimeSeconds(), destination.CredentialIssuedAt);
        Assert.Equal(test.Clock.Now.AddHours(1).ToUnixTimeSeconds(), destination.CredentialExpiresAt);
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(row => row.Event == CloneEvent, test.Token);
        Assert.Equal(clone.Application!.Id, audit.TargetId);
        var detail = await database.Set<ApplicationCloneAuditDetail>().AsNoTracking().SingleAsync(row => row.AuditId == audit.Id, test.Token);
        Assert.Equal(source.Application!.Id, detail.SourceApplicationId);
        Assert.Equal(source.Version, detail.SourceVersion);
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        Assert.Empty(await authorizations.FindByApplicationIdAsync(clone.Application.Id, test.Token).ToListAsync(test.Token));
        var retained = Assert.Single(await authorizations.FindByApplicationIdAsync(source.Application.Id, test.Token).ToListAsync(test.Token));
        Assert.Equal(ValidStatus, await authorizations.GetStatusAsync(retained, test.Token));
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        Assert.Empty(await tokens.FindByApplicationIdAsync(clone.Application.Id, test.Token).ToListAsync(test.Token));
        var retainedToken = Assert.Single(await tokens.FindByApplicationIdAsync(source.Application.Id, test.Token).ToListAsync(test.Token));
        Assert.Equal(ValidStatus, await tokens.GetStatusAsync(retainedToken, test.Token));
    }

    /// <summary>Seeds nonempty native source state as component setup, not evidence of protocol issuance; cloning must preserve it without inheritance.</summary>
    private static async Task SeedSourceGrantsAsync(ApplicationMutationFixture test, string id)
    {
        await using var scope = test.Scope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Subject = test.Identity.Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, test.Token);
        await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            AuthorizationId = await authorizations.GetIdAsync(authorization, test.Token),
            Subject = test.Identity.Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.AccessToken,
            CreationDate = test.Clock.Now.UtcDateTime,
            ExpirationDate = test.Clock.Now.AddMinutes(GrantMinutes).UtcDateTime
        }, test.Token);
    }

    /// <summary>Fingerprints every persisted native grant and token field, including identities, links, subjects, scopes and lifecycle timestamps.</summary>
    private static async Task<string> GrantsAsync(ApplicationMutationFixture test, string id)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
            .Where(row => row.Application!.Id == id).OrderBy(row => row.Id)
            .Select(row => new { Row = row, ApplicationId = EF.Property<string>(row, ApplicationKey) }).ToArrayAsync(test.Token);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Application!.Id == id).OrderBy(row => row.Id)
            .Select(row => new { Row = row, ApplicationId = EF.Property<string>(row, ApplicationKey), AuthorizationId = EF.Property<string>(row, AuthorizationKey) }).ToArrayAsync(test.Token);
        Assert.NotEmpty(authorizations);
        Assert.NotEmpty(tokens);
        return ApplicationUpdateSnapshot.Hash(new
        {
            Authorizations = authorizations,
            Tokens = tokens
        });
    }

    /// <summary>Freezes a new credential deadline independently of the source's lifecycle.</summary>
    private static ApplicationMutationCommand Command(ApplicationMutationFixture test, ApplicationMutationResult source, string client = Destination) =>
        ApplicationMutationCommand.Clone(source.Application!.Id, source.Version!,
            new(true, client, Display, test.Clock.Now.AddHours(1).ToString(ExpiryFormat, CultureInfo.InvariantCulture)), test.Clock.Now)!;
}
