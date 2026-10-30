using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Injects real provider failures after native saves and requires complete rollback with no reusable successful result.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationMutationFailureTests(BootstrapFixture fixture)
{
    private const int ExpiryAdvanceSeconds = 2;
    private const string PilotId = "synthetic-managed-protection";
    private const string PilotScope = "synthetic-pilot-scope";
    private const string PilotUser = "synthetic-pilot-user";
    private const string Edit = "Protected edit";

    /// <summary>Late revocation, missing audit, drift and expiry cannot commit a native application or policy; the failed output is never returned.</summary>
    [Theory]
    [InlineData("PostgreSql", ApplicationWriteDefect.AuditFailure, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationWriteDefect.TokenRevoked, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationWriteDefect.SourceRevoked, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationWriteDefect.AuditDeleted, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationWriteDefect.PolicyChanged, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationWriteDefect.CredentialExpired, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationWriteDefect.SourceRebound, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", ApplicationWriteDefect.SourceRebound, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationWriteDefect.AuditFailure, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationWriteDefect.TokenRevoked, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationWriteDefect.SourceRevoked, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationWriteDefect.AuditDeleted, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationWriteDefect.PolicyChanged, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationWriteDefect.CredentialExpired, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PostSaveFailure_When_CreateRuns_Then_AllNativePolicyAndAuditChangesRollBack(string provider, ApplicationWriteDefect defect)
    {
        var fault = new ApplicationWriteFault(defect);
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider, fault);
        fault.Advance = () => test.Clock.Now = test.Clock.Now.AddSeconds(ExpiryAdvanceSeconds);
        fault.Armed = true;
        var command = defect == ApplicationWriteDefect.CredentialExpired ? test.Create(lifetime: 1) : test.Create();
        if (defect is ApplicationWriteDefect.AuditFailure or ApplicationWriteDefect.AuditDeleted or ApplicationWriteDefect.PolicyChanged)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => test.ExecuteAsync(command));
        }
        else
        {
            var result = await test.ExecuteAsync(command);
            Assert.Equal(defect == ApplicationWriteDefect.CredentialExpired ? 400 : 401, result.Status);
            Assert.Null(result.Credential);
            Assert.Null(result.Application);
        }
        Assert.True(fault.Fired);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await database.Set<ApplicationPolicy>().AsNoTracking().ToArrayAsync(test.Token));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().FindByClientIdAsync(ApplicationMutationFixture.Client, test.Token));
        Assert.Equal(0, await test.AuditsAsync());
    }

    /// <summary>Persisted pilot identity protects the target even when its native name resembles an ordinary managed registration.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PersistedSystemBinding_When_TargetHasManagedMetadata_Then_EditAndReplacementStillDeny(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(new PilotManifest
        {
            Id = PilotId,
            ApplicationId = created.Application!.Id,
            UserId = test.Identity.Subject,
            ScopeId = PilotScope,
            UserName = PilotUser,
            RedirectUri = ApplicationMutationFixture.Redirect
        });
        await database.SaveChangesAsync(test.Token);
        var edit = ApplicationMutationCommand.Edit(created.Application.Id, created.Version!, new(Edit, [ApplicationMutationFixture.Redirect]))!;
        Assert.Equal(403, (await test.ExecuteAsync(edit)).Status);
        Assert.Equal(403, (await test.ExecuteAsync(test.Replace(created))).Status);
        Assert.True(await test.AcceptsAsync(created.Application.Id, created.Credential!.ClientSecret));
        Assert.Equal(1, await test.AuditsAsync());
    }
}
