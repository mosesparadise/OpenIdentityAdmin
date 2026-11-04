using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Applications.ApplicationHttpUpdateEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Accepts actual mapped edits/replacements with genuine HTTPS native credentials and exact detached aggregate observations on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationHttpUpdateTests(BootstrapFixture fixture)
{
    private const int ExpiredCreateMinutes = -20;
    private const int InitialSeconds = 1;
    private const int AdvanceSeconds = 2;
    private const int FutureRevision = 2;
    private const string True = "true";
    private const string False = "false";
    private static readonly string[] ImmutableFields = ["\"clientId\":\"immutable\",", "\"clientSecret\":\"secret-canary\",", "\"profile\":\"arbitrary\",", "\"credentialExpiresAt\":\"2099-01-01T00:00:00Z\","];
    private const string Missing = "missing-application";
    private const string Wildcard = "*";
    private const string Weak = "W/";
    private const string Invalid = "invalid";
    private const string Comma = ",";
    private const string SecretField = "clientSecret";

    /// <summary>An expired old credential does not prevent PUT or recovery; replacement invalidates its actual hash and subsequent reads cannot recover plaintext.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExpiredManagedCredential_When_HttpsEditThenReplacementRuns_Then_ExactNativeStateAndOneTimeRecoveryArePublished(string provider, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now.AddMinutes(ExpiredCreateMinutes).AddSeconds(InitialSeconds)), direct: direct);
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var id = created.Application.Id;
        test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(AdvanceSeconds);
        var before = await SnapshotAsync(test, id);
        using var edit = await test.UpdateAsync(id, Edit, create.Headers.ETag!.ToString(), direct: direct);
        await RequireAsync(test, before, edit, Edit, false, created.ClientSecret);
        var edited = await SnapshotAsync(test, id);
        var body = Replacement(test.Now);
        using var replacement = await test.UpdateAsync(id, body, edit.Headers.ETag!.ToString(), replacement: true, direct: direct);
        var secret = await RequireAsync(test, edited, replacement, body, true, created.ClientSecret);
        var committed = await SnapshotAsync(test, id);
        using var replay = await test.UpdateAsync(id, body, edit.Headers.ETag.ToString(), replacement: true, direct: direct);
        await ApplicationHttpTests.ProblemAsync(replay, HttpStatusCode.PreconditionFailed);
        Assert.Equal(committed.Digest, (await SnapshotAsync(test, id)).Digest);
        foreach (var path in new[] { ApplicationHttpTests.Path(id), ApplicationHttpFixture.Collection })
        {
            using var read = await test.Browser.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var text = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.False(text.Contains(secret, StringComparison.Ordinal));
            Assert.False(text.Contains(SecretField, StringComparison.Ordinal));
        }
        Assert.Equal(committed.Digest, (await SnapshotAsync(test, id)).Digest);
    }

    /// <summary>Missing, weak, wildcard, list, malformed, foreign-resource and stale versions preserve all native/policy/audit fields before valid recovery.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_CurrentManagedAggregate_When_HttpPreconditionsAreInvalid_Then_NoActionMutatesAndOriginalVersionRemainsUsable(string provider, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var id = created.Application.Id;
        var version = create.Headers.ETag!.ToString();
        var before = await SnapshotAsync(test, id);
        foreach (var replacement in new[] { false, true })
        {
            foreach (var tag in new[] { null, Weak + version, Wildcard, version + Comma + version, Invalid, ApplicationVersion.Create(Missing, 1), ApplicationVersion.Create(id, FutureRevision) })
            {
                using var denied = await test.UpdateAsync(id, replacement ? Replacement(test.Now) : Edit, tag, replacement, direct);
                await ApplicationHttpTests.ProblemAsync(denied, tag is null ? HttpStatusCode.PreconditionRequired : HttpStatusCode.PreconditionFailed);
                Assert.Equal(before.Digest, (await SnapshotAsync(test, id)).Digest);
            }
        }
        using var accepted = await test.UpdateAsync(id, Edit, version, direct: direct);
        await RequireAsync(test, before, accepted, Edit, false, created.ClientSecret);
        var committed = await SnapshotAsync(test, id);
        using var stale = await test.UpdateAsync(id, Edit, version, direct: direct);
        await ApplicationHttpTests.ProblemAsync(stale, HttpStatusCode.PreconditionFailed);
        Assert.Equal(committed.Digest, (await SnapshotAsync(test, id)).Digest);
    }

    /// <summary>Immutable fields, absent explicit confirmation, expired expiry and protected/missing identities fail through both real route surfaces.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_HttpUpdates_When_InputOrTargetIsUnsupported_Then_ExactAggregateIsPreserved(string provider, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var id = created.Application.Id;
        var version = create.Headers.ETag!.ToString();
        var before = await SnapshotAsync(test, id);
        foreach (var extra in ImmutableFields)
        {
            using var denied = await test.UpdateAsync(id, Edit.Insert(1, extra), version, direct: direct);
            await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.BadRequest);
        }
        foreach (var body in new[] { Replacement(test.Now).Replace(True, False, StringComparison.Ordinal), Replacement(test.Now.AddHours(-1)) })
        {
            using var denied = await test.UpdateAsync(id, body, version, replacement: true, direct: direct);
            await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.BadRequest);
        }
        await using var scope = test.Scope();
        var system = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorProtocolProfile>()
            .Select(row => row.ApplicationId).SingleAsync(TestContext.Current.CancellationToken);
        var protectedBefore = await NativeDigestAsync(test, system);
        foreach (var replacement in new[] { false, true })
        {
            foreach (var target in new[] { system, Missing, id.ToUpperInvariant() })
            {
                Assert.NotEqual(id, target);
                using var denied = await test.UpdateAsync(target, replacement ? Replacement(test.Now) : Edit, ApplicationVersion.Create(target, 1), replacement, direct);
                await ApplicationHttpTests.ProblemAsync(denied, target == system ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound);
            }
        }
        Assert.Equal(protectedBefore, await NativeDigestAsync(test, system));
        Assert.Equal(before.Digest, (await SnapshotAsync(test, id)).Digest);
    }
}
