using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Owns genuinely enrolled current authority and active native registration setup for retirement component tests.</summary>
internal sealed class ApplicationRetirementTestContext(ApplicationMutationFixture fixture, ApplicationMutationResult application) : IAsyncDisposable
{
    private const int Created = 201;

    private const string Reason = "security";
    private const string GuidFormat = "D";
    internal ApplicationMutationFixture Fixture => fixture;
    internal ApplicationMutationResult Application => application;
    internal string Id => application.Application!.Id;
    internal CancellationToken Token => fixture.Token;
    internal GrantTestContext Grants { get; } = new(fixture, application);

    /// <summary>Creates an enabled managed registration through its production coordinator; ordinary disable is not used as retirement setup.</summary>
    internal static async Task<ApplicationRetirementTestContext> CreateAsync(BootstrapFixture bootstrap, string provider, IInterceptor? fault = null)
    {
        var fixture = await ApplicationMutationFixture.CreateAsync(bootstrap, provider, fault);
        try
        {
            var application = await fixture.ExecuteAsync(fixture.Create());
            Assert.Equal(Created, application.Status);
            Assert.True(application.Application!.Enabled);
            return new(fixture, application);
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    /// <summary>Freezes the original exact application version, finite reason and independently generated canonical key.</summary>
    internal ApplicationRetirementIntent Intent() => ApplicationRetirementIntent.Create(Id, application.Version!, Guid.NewGuid().ToString(GuidFormat), new(Reason, true))!;

    /// <summary>Executes the actual owned coordinator under current authority, retaining the supplied intent for explicit replay tests.</summary>
    internal async Task<ApplicationRetirementAcceptance> RetireAsync(ApplicationRetirementIntent intent, ServiceProvider? node = null, ApiIdentity? identity = null)
    {
        node ??= fixture.Api;
        using var budget = new ApplicationReadBudget(fixture.Clock, Token);
        return await ApplicationRetirementOperation.ExecuteAsync(node.GetRequiredService<IServiceScopeFactory>(), identity ?? fixture.Identity, intent,
            node.GetRequiredService<ApiResourceProfile>(), fixture.Clock, budget, Guid.NewGuid());
    }

    /// <summary>Reads the immutable terminal receipt independently after transaction cleanup.</summary>
    internal async Task<ApplicationRetirement> ReceiptAsync()
    {
        await using var scope = fixture.Scope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationRetirement>().AsNoTracking().SingleAsync(Token);
    }

    /// <summary>Includes retained clone provenance and retirement receipts alongside all native/grant/audit state in rollback comparisons.</summary>
    internal async Task<string> SnapshotAsync()
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return ApplicationUpdateSnapshot.Hash(new
        {
            GrantState = await GrantBoundaryEvidence.SnapshotAsync(database, Id),
            Retirement = await database.Set<ApplicationRetirement>().AsNoTracking().OrderBy(row => row.ApplicationId).ToArrayAsync(Token),
            Clones = await database.Set<ApplicationCloneAuditDetail>().AsNoTracking().OrderBy(row => row.AuditId).ToArrayAsync(Token)
        });
    }

    /// <summary>Releases the one owned authority fixture; the helper grant view does not own a second fixture.</summary>
    public ValueTask DisposeAsync() => fixture.DisposeAsync();
}
