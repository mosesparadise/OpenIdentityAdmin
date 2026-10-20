using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises the real redirected one-shot Migrator command, safe diagnostics and committed state on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ProtocolInitializationCommandTests(BootstrapFixture fixture)
{
    private const string Operation = "initialize-administrator-protocol";
    private const string Operator = "native-command-fixture";
    private const string IdentifierFormat = "D";
    private const string AuditEvent = "administrator-protocol-initialization";
    private const string InvalidDocument = "{\"synthetic-untrusted-input\":";
    private const string Created = "Administrator protocol registration initialized.";
    private const string Unchanged = "Administrator protocol registration already initialized.";
    private const string Invalid = "Invalid administrator protocol initialization input.";
    private const string Failed = "Administrator protocol initialization failed. Check restricted operational diagnostics.";
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>Initialization and an exact repeat run in distinct real processes; independent reads prove atomic creation and no repair writes.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_CurrentSchemaAndPublicInput_When_OneShotCommandRunsAndRepeats_Then_RegistrationIsAtomicAndIdempotent(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        using var material = new NativeAssertionMaterial();
        var input = Input(material);
        var first = await RunAsync(database, provider, input);
        Assert.Equal(0, first.ExitCode);
        Assert.True(first.Output.Contains(Created, StringComparison.Ordinal));
        var original = await SnapshotAsync(database);
        Assert.Equal((1, 1, 1, 1), original.Counts);
        var repeated = await RunAsync(database, provider, input);
        Assert.Equal(0, repeated.ExitCode);
        Assert.True(repeated.Output.Contains(Unchanged, StringComparison.Ordinal));
        Assert.Equal(original, await SnapshotAsync(database));
    }

    /// <summary>Malformed redirected input never opens native registration or echoes submitted data.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_MalformedPublicInput_When_OneShotCommandRuns_Then_SafeInputFailureLeavesNoRegistration(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        var result = await RunAsync(database, provider, System.Text.Encoding.UTF8.GetBytes(InvalidDocument));
        Assert.Equal(2, result.ExitCode);
        Assert.True(result.Output.Contains(Invalid, StringComparison.Ordinal));
        Assert.False(result.Output.Contains(InvalidDocument, StringComparison.Ordinal));
        Assert.Equal((0, 0, 0, 0), (await SnapshotAsync(database)).Counts);
    }

    /// <summary>Denied audit insertion fails the real command and rolls back app, scope and manifest rather than returning partial success.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_AuditInsertDenied_When_OneShotCommandRuns_Then_SafeFailureRollsBackAllRegistrationState(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        using var material = new NativeAssertionMaterial();
        await database.AuditPermissionAsync(false, TestContext.Current.CancellationToken);
        var result = await RunAsync(database, provider, Input(material));
        Assert.Equal(1, result.ExitCode);
        Assert.True(result.Output.Contains(Failed, StringComparison.Ordinal));
        Assert.False(result.Output.Contains(database.RuntimeConnection, StringComparison.Ordinal));
        Assert.Equal((0, 0, 0, 0), (await SnapshotAsync(database)).Counts);
    }

    /// <summary>Writes only public material to redirected stdin; existing process harness occupies a port to prove the command opens no listener.</summary>
    private static async Task<(int ExitCode, string Output)> RunAsync(BootstrapDatabase database, string provider, byte[] input)
    {
        await using var process = new BootstrapProcess(provider, database.RuntimeConnection, Operation);
        await process.SendAsync(input);
        return await process.CompleteAsync();
    }

    /// <summary>Serializes the exact declared public initialization document without any private RSA or account credential.</summary>
    private static byte[] Input(NativeAssertionMaterial material)
    {
        var key = material.Policy.Key.CreateNativeKey();
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            operationId = Guid.NewGuid().ToString(IdentifierFormat),
            operatorReference = Operator,
            redirectUri = material.Policy.RedirectUri,
            publicKey = new
            {
                kty = key.Kty,
                kid = key.Kid,
                alg = key.Alg,
                use = key.Use,
                n = key.N,
                e = key.E
            }
        });
    }

    /// <summary>Reads all four independent durable effects with an administrator context after actual child-process exit and cleanup.</summary>
    private static async Task<Snapshot> SnapshotAsync(BootstrapDatabase database)
    {
        await using var scope = database.Administrator.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var profile = await context.Set<AdministratorProtocolProfile>().AsNoTracking().SingleOrDefaultAsync(token);
        var audit = await context.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Event == AuditEvent, token);
        return new((await context.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync(token),
            await context.Set<OpenIddictEntityFrameworkCoreScope>().CountAsync(token), profile is null ? 0 : 1, audit is null ? 0 : 1),
            profile?.ApplicationId, profile?.ScopeId, profile?.Revision, profile?.LockRevision, profile?.ObservedAt, audit?.Id, audit?.CorrelationId);
    }

    public static IEnumerable<TheoryDataRow<string>> ProviderCases => Providers.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));

    /// <summary>Compares durable identities/revisions without retaining protected payloads or database credentials.</summary>
    private sealed record Snapshot((int Applications, int Scopes, int Profiles, int Audits) Counts, string? Application, string? Scope,
        long? Revision, long? LockRevision, long? ObservedAt, Guid? Audit, Guid? Correlation);
}
