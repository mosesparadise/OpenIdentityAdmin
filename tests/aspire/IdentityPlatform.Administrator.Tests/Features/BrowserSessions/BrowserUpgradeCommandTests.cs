using System.Text;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises the real explicit Migrator process, redirected public input, safe output and durable audit atomicity.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserUpgradeCommandTests(BootstrapFixture fixture)
{
    private const string OperationId = "operationId";
    private const string OperatorReference = "operatorReference";
    private const string RedirectUri = "redirectUri";
    private const string PublicKey = "publicKey";
    private const string Initialize = "initialize-administrator-protocol";
    private const string Upgraded = "Administrator browser registration upgraded.";
    private const string Reconciled = "Administrator browser registration upgrade already applied.";
    private const string Failed = "Administrator browser registration upgrade failed. Check restricted operational diagnostics.";
    private const string Invalid = "Invalid administrator browser registration upgrade input.";
    private const string InvalidInput = "{\"synthetic-invalid-upgrade\":";
    private const string Revision = "expectedRevision";
    private const string ProviderTrait = "Provider";
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>Separate processes perform one transition and exact reconciliation while the harness holds the configured listener port occupied.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_InitializedProfile_When_ExplicitUpgradeCommandRunsTwice_Then_OnlyOneAuditedTransitionPersists(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        using var material = new NativeAssertionMaterial();
        Assert.Equal(0, (await RunAsync(database, provider, Initialize, Document(material, false))).ExitCode);
        var input = Document(material, true);
        var first = await RunAsync(database, provider, BrowserUpgradeFixture.Operation, input);
        Assert.Equal(0, first.ExitCode);
        Assert.True(first.Output.Contains(Upgraded, StringComparison.Ordinal));
        var after = await BrowserUpgradeFixture.ReadAsync(database.Administrator, material.Policy);
        Assert.True(after.Browser && !after.Legacy && after.Enabled);
        Assert.Equal(2, after.Revision);
        var repeated = await RunAsync(database, provider, BrowserUpgradeFixture.Operation, input);
        Assert.Equal(0, repeated.ExitCode);
        Assert.True(repeated.Output.Contains(Reconciled, StringComparison.Ordinal));
        Assert.True(after == await BrowserUpgradeFixture.ReadAsync(database.Administrator, material.Policy));
    }

    /// <summary>Denied audit insertion rolls native metadata/profile changes back and the command emits no credential-bearing provider diagnostics.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_DeniedAuditInsert_When_ExplicitUpgradeCommandRuns_Then_AllStateRollsBackWithSafeFailure(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        using var material = new NativeAssertionMaterial();
        Assert.Equal(0, (await RunAsync(database, provider, Initialize, Document(material, false))).ExitCode);
        var before = await BrowserUpgradeFixture.ReadAsync(database.Administrator, material.Policy);
        await database.AuditPermissionAsync(false, TestContext.Current.CancellationToken);
        var result = await RunAsync(database, provider, BrowserUpgradeFixture.Operation, Document(material, true));
        Assert.Equal(1, result.ExitCode);
        Assert.True(result.Output.Contains(Failed, StringComparison.Ordinal));
        Assert.False(result.Output.Contains(database.RuntimeConnection, StringComparison.Ordinal));
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(database.Administrator, material.Policy));
    }

    /// <summary>Malformed input fails through the command parser without reflecting submitted content or repairing a missing registration.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_InvalidUpgradeInput_When_CommandRuns_Then_OnlyTheSafeInputFailureIsReported(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        var result = await RunAsync(database, provider, BrowserUpgradeFixture.Operation, Encoding.UTF8.GetBytes(InvalidInput));
        Assert.Equal(2, result.ExitCode);
        Assert.True(result.Output.Contains(Invalid, StringComparison.Ordinal));
        Assert.False(result.Output.Contains(InvalidInput, StringComparison.Ordinal));
        using var material = new NativeAssertionMaterial();
        var missing = await RunAsync(database, provider, BrowserUpgradeFixture.Operation, Document(material, true));
        Assert.Equal(1, missing.ExitCode);
        Assert.True(missing.Output.Contains(Failed, StringComparison.Ordinal));
    }

    /// <summary>Unavailable receipt reads deny before metadata mutation and leave independently readable administrator state unchanged.</summary>
    [Theory, MemberData(nameof(ProviderCases))]
    public async Task Given_DeniedAuditRead_When_UpgradeCommandRuns_Then_ItCannotInventAReceiptOrChangeRegistration(string provider)
    {
        await using var database = await fixture.LeaseAsync(provider);
        using var material = new NativeAssertionMaterial();
        Assert.Equal(0, (await RunAsync(database, provider, Initialize, Document(material, false))).ExitCode);
        var before = await BrowserUpgradeFixture.ReadAsync(database.Administrator, material.Policy);
        await DenyAuditReadAsync(database, provider);
        var result = await RunAsync(database, provider, BrowserUpgradeFixture.Operation, Document(material, true));
        Assert.Equal(1, result.ExitCode);
        Assert.True(result.Output.Contains(Failed, StringComparison.Ordinal));
        Assert.False(result.Output.Contains(database.RuntimeConnection, StringComparison.Ordinal));
        Assert.True(before == await BrowserUpgradeFixture.ReadAsync(database.Administrator, material.Policy));
    }

    /// <summary>Uses only disposable administrator credentials to remove the command role's explicit receipt-read privilege.</summary>
    private static async Task DenyAuditReadAsync(BootstrapDatabase database, string provider)
    {
        const string PostgreSql = "PostgreSql";
        const string Postgres = "REVOKE SELECT ON platform.\"SecurityAuditIntent\" FROM platform_runtime";
        const string SqlServer = "DENY SELECT ON OBJECT::[platform].[SecurityAuditIntent] TO [platform_runtime]";
        await using var scope = database.Administrator.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.ExecuteSqlRawAsync(provider == PostgreSql ? Postgres : SqlServer, TestContext.Current.CancellationToken);
    }

    /// <summary>Uses the existing real-process harness with no Sessions credentials, listener or private key input.</summary>
    private static async Task<(int ExitCode, string Output)> RunAsync(BootstrapDatabase database, string provider, string operation, byte[] input)
    {
        await using var process = new BootstrapProcess(provider, database.RuntimeConnection, operation);
        await process.SendAsync(input);
        return await process.CompleteAsync();
    }

    /// <summary>Builds only the declared public input, retaining one exact byte document for same-operation replay.</summary>
    private static byte[] Document(NativeAssertionMaterial material, bool upgrade)
    {
        var key = material.Policy.Key.CreateNativeKey();
        Dictionary<string, object> fields = new()
        {
            [OperationId] = Guid.NewGuid().ToString(),
            [OperatorReference] = BrowserUpgradeFixture.Operator,
            [RedirectUri] = material.Policy.RedirectUri,
            [PublicKey] = new
            {
                kty = key.Kty,
                kid = key.Kid,
                alg = key.Alg,
                use = key.Use,
                n = key.N,
                e = key.E
            }
        };
        if (upgrade)
        {
            fields.Add(Revision, 1);
        }
        return JsonSerializer.SerializeToUtf8Bytes(fields);
    }

    public static IEnumerable<TheoryDataRow<string>> ProviderCases => Providers.Select(provider => new TheoryDataRow<string>(provider).WithTrait(ProviderTrait, provider));
}
