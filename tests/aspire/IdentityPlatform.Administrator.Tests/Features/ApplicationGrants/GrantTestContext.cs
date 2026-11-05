using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.ApplicationGrants;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Uses genuine enrolled current API authority and manager-created native state at the component boundary; it does not claim HTTP/browser validation.</summary>
internal sealed class GrantTestContext(ApplicationMutationFixture fixture, ApplicationMutationResult application) : IAsyncDisposable
{
    private const int Created = 201;
    private const int Ok = 200;
    private const string GuidFormat = "D";

    private const string Reason = "security";
    private const string Subject = "synthetic-grant-subject";
    internal ApplicationMutationFixture Fixture => fixture;
    internal ApplicationMutationResult Application => application;
    internal string Id => application.Application!.Id;
    internal CancellationToken Token => fixture.Token;

    /// <summary>Commits creation and disable through their production coordinators before any revocation request exists.</summary>
    internal static async Task<GrantTestContext> CreateAsync(BootstrapFixture bootstrap, string provider, IInterceptor? fault = null)
    {
        var fixture = await ApplicationMutationFixture.CreateAsync(bootstrap, provider, fault);
        try
        {
            var created = await fixture.ExecuteAsync(fixture.Create());
            Assert.Equal(Created, created.Status);
            var disabled = await fixture.ExecuteAsync(ApplicationMutationCommand.Lifecycle(created.Application!.Id, created.Version!, new(Reason, true), false)!);
            Assert.Equal(Ok, disabled.Status);
            return new(fixture, disabled);
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    /// <summary>Creates explicit immutable submission input; tests can reuse its exact key without hiding retries in transport.</summary>
    internal GrantIntent Intent() => GrantIntent.Create(Id, application.Version!, Guid.NewGuid().ToString(GuidFormat), new(Reason, true))!;

    /// <summary>Exercises actual current grant/MFA and receipt ownership with the fixture's original account/session/native rows.</summary>
    internal Task<GrantJobView> AdmitAsync(GrantIntent intent) => AdmitOnAsync(fixture.Api, intent);

    /// <summary>Runs the same current authorization on an independent API root for provider-observed idempotency races.</summary>
    internal async Task<GrantJobView> AdmitOnAsync(ServiceProvider node, GrantIntent intent)
    {
        using var budget = new ApplicationReadBudget(fixture.Clock, Token);
        return await GrantAdmissionOperation.ExecuteAsync(node.GetRequiredService<IServiceScopeFactory>(), fixture.Identity, intent,
            node.GetRequiredService<ApiResourceProfile>(), fixture.Clock, budget, Guid.NewGuid());
    }

    /// <summary>Runs a new actual owned request for every batch rather than reusing request/session authority.</summary>
    internal Task<bool> BatchAsync(string id) => GrantBatchOperation.ExecuteAsync(fixture.Api.GetRequiredService<IServiceScopeFactory>(), Guid.Parse(id), fixture.Clock, Token);

    /// <summary>Observes persisted progress through a separate context after the operation has completed disposal.</summary>
    internal async Task<ApplicationGrantJob> ReadAsync(string id)
    {
        await using var scope = fixture.Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationGrantJob>().AsNoTracking()
            .SingleAsync(row => row.Id == Guid.Parse(id), Token);
    }

    /// <summary>Creates independent native families including authorization-less cached request tokens using real managers.</summary>
    internal async Task SeedAsync(string id, int authorizationCount, int tokenCount)
    {
        await using var scope = fixture.Scope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        string? first = null;
        for (var index = 0; index < authorizationCount; index++)
        {
            var row = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = id,
                Subject = Subject,
                Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent
            }, Token);
            first ??= await authorizations.GetIdAsync(row, Token);
        }
        for (var index = 0; index < tokenCount; index++)
        {
            await tokens.CreateAsync(new OpenIddictTokenDescriptor
            {
                ApplicationId = id,
                AuthorizationId = index == 0 ? first : null,
                Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken
            }, Token);
        }
    }

    /// <summary>Reads exact statuses independently of the manager mutation result and job counters.</summary>
    internal async Task<(string?[] Authorizations, string?[] Tokens)> StatusesAsync(string id)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return (await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().Where(row => row.Application!.Id == id)
            .OrderBy(row => row.Id).Select(row => row.Status).ToArrayAsync(Token),
            await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Where(row => row.Application!.Id == id)
            .OrderBy(row => row.Id).Select(row => row.Status).ToArrayAsync(Token));
    }

    /// <summary>Captures exact native identity, association and status from fresh storage without retaining payloads, subjects or credential material.</summary>
    internal async Task<string> NativeSnapshotAsync()
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, ApplicationId = row.Application!.Id, row.Status, row.Type }).ToArrayAsync(Token);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, ApplicationId = row.Application!.Id, AuthorizationId = row.Authorization!.Id, row.Status, row.Type }).ToArrayAsync(Token);
        return JsonSerializer.Serialize(new
        {
            Authorizations = authorizations,
            Tokens = tokens
        });
    }

    /// <summary>Disposes native contexts and synthetic credential material using the existing fixture's ownership order.</summary>
    public ValueTask DisposeAsync() => fixture.DisposeAsync();
}
