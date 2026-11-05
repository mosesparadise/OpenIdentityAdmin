using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Checks real persisted lifecycle transitions and unchanged native credentials through the current-authority component boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationLifecycleTests(BootstrapFixture fixture)
{
    private const string Bypass = "application-lifecycle-revision-bypass";
    private const int Created = 201;
    private const int Updated = 200;
    private const string Maintenance = "maintenance";
    private const string Security = "security";
    private const string EditedDisplay = "Edited disabled application";
    private const string EditedCallback = "https://disabled.example.test/callback";

    /// <summary>Both transitions advance only aggregate authority, retain native credentials, and atomically attribute one finite reason to each audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql", "CriticalDecision", "application-lifecycle-revision"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer", "CriticalDecision", "application-lifecycle-revision"])]
    public async Task Given_ManagedRegistration_When_DisabledAndEnabled_Then_VersionsAdvanceWithoutCredentialOrNativeChanges(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        Assert.Equal(201, created.Status);
        var id = created.Application!.Id;
        var before = await ApplicationUpdateSnapshot.ReadAsync(test, id);
        var version = created.Version!;
        foreach (var enabled in new[] { false, true })
        {
            var command = ApplicationMutationCommand.Lifecycle(id, version, new(Maintenance, true), enabled)!;
            var correlation = Guid.NewGuid();
            var changed = await test.ExecuteOnAsync(test.Api, command, correlation, test.Token);
            Assert.Equal(200, changed.Status);
            Assert.Equal(enabled, changed.Application!.Enabled);
            Assert.Null(changed.Credential);
            var after = await ApplicationUpdateSnapshot.ReadAsync(test, id);
            Assert.True(changed.Version != version, Bypass);
            ApplicationLifecycleOracle.Require(test, before, after, enabled, correlation);
            ApplicationLifecycleOracle.Output(changed, after);
            Assert.Equal(412, (await test.ExecuteAsync(command)).Status);
            var repeated = ApplicationMutationCommand.Lifecycle(id, changed.Version!, new(Security, true), enabled)!;
            Assert.Equal(409, (await test.ExecuteAsync(repeated)).Status);
            Assert.Equal(after.Digest, (await ApplicationUpdateSnapshot.ReadAsync(test, id)).Digest);
            version = changed.Version!;
            before = after;
        }
        Assert.True(await test.AcceptsAsync(id, created.Credential!.ClientSecret));
    }

    /// <summary>Creates a managed registration and, for enable tests, commits the prerequisite disable through the actual coordinator.</summary>
    internal static async Task<ApplicationMutationResult> PrepareAsync(ApplicationMutationFixture test, bool enabled)
    {
        var current = await test.ExecuteAsync(test.Create());
        Assert.Equal(Created, current.Status);
        if (enabled)
        {
            current = await test.ExecuteAsync(ApplicationMutationCommand.Lifecycle(current.Application!.Id, current.Version!, new(Maintenance, true), false)!);
            Assert.Equal(Updated, current.Status);
        }
        return current;
    }

    /// <summary>Editing and replacing a disabled registration preserves Disabled while retaining exact native credential and reason-history semantics.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DisabledApplication_When_EditedAndCredentialReplaced_Then_ItStaysDisabledAndOnlyRequestedFieldsChange(string provider)
    {
        await using var test = await ApplicationMutationFixture.CreateAsync(fixture, provider);
        var created = await test.ExecuteAsync(test.Create());
        var current = await test.ExecuteAsync(ApplicationMutationCommand.Lifecycle(created.Application!.Id, created.Version!, new(Maintenance, true), false)!);
        Assert.Equal(Updated, current.Status);
        foreach (var replacement in new[] { false, true })
        {
            var before = await ApplicationUpdateSnapshot.ReadAsync(test, current.Application!.Id);
            var command = replacement ? test.Replace(current) : ApplicationMutationCommand.Edit(current.Application.Id, current.Version!,
                new(EditedDisplay, [EditedCallback]))!;
            var correlation = Guid.NewGuid();
            current = await test.ExecuteOnAsync(test.Api, command, correlation, test.Token);
            await ApplicationUpdateOracle.RequireAsync(test, before, command, current, correlation);
            Assert.False(current.Application!.Enabled);
            Assert.Equal(!replacement, await test.AcceptsAsync(current.Application.Id, created.Credential!.ClientSecret));
        }
    }
}
