using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Extends genuine HTTP admission negatives with exact target/version and persisted pilot protection, independently checking rollback and a final valid control.</summary>
internal static class GrantTargetHttpEvidence
{
    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string GuidFormat = "D";
    private const string PilotId = "grant-target-protection";
    private const string PilotScope = "synthetic-grant-scope";
    private const string Subject = "synthetic-grant-subject";
    private const string ReservedPilot = "identityplatform-pilot";
    private const string ReservedAdministrator = "identity-admin-bff";
    private const string PeerId = "different-grant-target";
    private const long OriginalRevision = 1;

    /// <summary>Uses both real HTTP surfaces for each negative; persisted defects are removed before valid acceptance proves current caller authority.</summary>
    internal static async Task RequireAsync(ApplicationHttpFixture test, string id, string version)
    {
        await SeedAsync(test, id);
        await DeniedAsync(test, id, IdentityPlatform.Core.Features.Applications.ApplicationVersion.Create(id, OriginalRevision), HttpStatusCode.PreconditionFailed);
        await DeniedAsync(test, id, IdentityPlatform.Core.Features.Applications.ApplicationVersion.Create(PeerId, OriginalRevision), HttpStatusCode.PreconditionFailed);
        await DeniedAsync(test, Guid.NewGuid().ToString(GuidFormat), version, HttpStatusCode.NotFound);
        await PolicyDenialsAsync(test, id, version);
        await NativeProfileDenialAsync(test, id, version);
        await PilotDenialAsync(test, id, version);
        using var accepted = await test.GrantAsync(id, Body, version, Guid.NewGuid().ToString(GuidFormat));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
    }

    /// <summary>Provides actual eligible native rows so a denied operation cannot hide accidental revocation behind an empty target.</summary>
    private static async Task SeedAsync(ApplicationHttpFixture test, string id)
    {
        await using var scope = test.Scope();
        await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>().CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Subject = Subject,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        }, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = id,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Temporarily removes managed ownership and restores the exact policy row without weakening relational constraints.</summary>
    private static async Task PolicyDenialsAsync(ApplicationHttpFixture test, string id, string version)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var policy = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == id, token);
        Assert.Equal(1, await database.Set<ApplicationPolicy>().Where(row => row.ApplicationId == id).ExecuteDeleteAsync(token));
        try
        {
            await DeniedAsync(test, id, version, HttpStatusCode.Conflict);
        }
        finally
        {
            database.Add(policy);
            await database.SaveChangesAsync(token);
        }
    }

    /// <summary>Removes the native PKCE requirement through the actual manager, retaining valid schema but leaving the supported managed profile.</summary>
    private static async Task NativeProfileDenialAsync(ApplicationHttpFixture test, string id, string version)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = test.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = (await manager.FindByIdAsync(id, token))!;
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        Assert.True(descriptor.Requirements.Remove(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange));
        await manager.UpdateAsync(application, descriptor, token);
        try
        {
            await DeniedAsync(test, id, version, HttpStatusCode.Conflict);
        }
        finally
        {
            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            await manager.UpdateAsync(application, descriptor, token);
        }
    }

    /// <summary>Binds an ordinary managed name by persisted pilot identity, preventing a name-based check from satisfying this independent protection oracle.</summary>
    private static async Task PilotDenialAsync(ApplicationHttpFixture test, string id, string version)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var application = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == id, token);
        Assert.False(string.Equals(application.ClientId, ReservedPilot, StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals(application.ClientId, ReservedAdministrator, StringComparison.OrdinalIgnoreCase));
        var binding = new PilotManifest
        {
            Id = PilotId,
            ApplicationId = id,
            UserId = test.Environment.Issuer.Account.ActorId,
            ScopeId = PilotScope,
            UserName = Subject,
            RedirectUri = ApplicationMutationFixture.Redirect
        };
        database.Add(binding);
        await database.SaveChangesAsync(token);
        try
        {
            await DeniedAsync(test, id, version, HttpStatusCode.Forbidden);
        }
        finally
        {
            database.Remove(binding);
            await database.SaveChangesAsync(token);
        }
    }

    /// <summary>Requires the same precise denial through API and BFF, with unchanged stored state and no job, approval or grant audit after each attempt.</summary>
    private static async Task DeniedAsync(ApplicationHttpFixture test, string id, string version, HttpStatusCode expected)
    {
        var before = await SnapshotAsync(test);
        foreach (var direct in new[] { false, true })
        {
            using var response = await test.GrantAsync(id, Body, version, Guid.NewGuid().ToString(GuidFormat), direct);
            await ApplicationHttpTests.ProblemAsync(response, expected);
            Assert.Equal(before, await SnapshotAsync(test));
            await using var scope = test.Scope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var token = TestContext.Current.CancellationToken;
            Assert.False(await database.Set<ApplicationGrantJob>().AnyAsync(token));
            Assert.False(await database.Set<ApplicationGrantApproval>().AnyAsync(token));
            Assert.False(await database.Set<ApplicationGrantAudit>().AnyAsync(token));
        }
    }

    /// <summary>Hashes independently reloaded native configuration, policies, native lifecycle and security audit; secret-bearing fields never enter assertion output.</summary>
    private static async Task<string> SnapshotAsync(ApplicationHttpFixture test)
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token);
        var fields = native.Select(row => database.Entry(row).Properties.OrderBy(value => value.Metadata.Name, StringComparer.Ordinal)
            .ToDictionary(value => value.Metadata.Name, value => value.CurrentValue)).ToArray();
        var policies = await database.Set<ApplicationPolicy>().AsNoTracking().OrderBy(row => row.ApplicationId).ToArrayAsync(token);
        var nativeGrants = await ManagedJourneyEvidence.NativeAsync(database);
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token);
        return ApplicationUpdateSnapshot.Hash(new
        {
            fields,
            policies,
            nativeGrants,
            audits
        });
    }
}
