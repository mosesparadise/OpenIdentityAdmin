using System.Net;
using System.Net.Http.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IdentityPlatform.Administrator.Tests.Features.Applications.ApplicationHttpUpdateEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Exercises real protected lifecycle API/BFF routes with native HTTPS credentials, strict wire input and current persisted authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationLifecycleHttpTests(BootstrapFixture fixture)
{
    private const string Body = "{\"reason\":\"maintenance\",\"confirm\":true}";
    private const string SecurityBody = "{\"reason\":\"security\",\"confirm\":true}";
    private const string ConfigurationBody = "{\"reason\":\"configuration\",\"confirm\":true}";
    private const string DisableEvent = "ApplicationDisabled";
    private const string EnableEvent = "ApplicationEnabled";
    private const string Permission = "applications.disable";
    private const string Foreign = "https://foreign.example.test";
    private const string InvalidProof = "invalid-native-csrf";
    private const string Missing = "missing-lifecycle-application";
    private const string Wildcard = "*";
    private const string Weak = "W/";
    private const string Comma = ",";
    private static readonly string[] InvalidBodies =
    [
        "{}", "{\"reason\":\"maintenance\"}", "{\"reason\":\"maintenance\",\"confirm\":false}",
        "{\"reason\":\"Maintenance\",\"confirm\":true}", "{\"reason\":\"maintenance \",\"confirm\":true}",
        "{\"reason\":\" maintenance\",\"confirm\":true}", "{\"reason\":\"arbitrary\",\"confirm\":true}",
        "{\"reason\":\"maintenance\",\"confirm\":\"true\"}", "{\"reason\":\"maintenance\",\"confirm\":true,\"enabled\":true}",
        "{\"reason\":\"maintenance\",\"reason\":\"security\",\"confirm\":true}",
        "{\"reason\":\"maintenance\",\"confirm\":true,\"clientSecret\":\"never-accepted\"}"
    ];

    /// <summary>Malformed bodies/tags and protected targets cannot alter the aggregate; the original exact version then disables and re-enables successfully.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ProtectedLifecycleRoutes_When_InputVersionOrTargetIsInvalid_Then_NoMutationPrecedesExplicitValidRecovery(string provider, bool direct)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var id = created.Application.Id;
        var version = create.Headers.ETag!.Tag;
        foreach (var enabled in new[] { false, true })
        {
            var before = await SnapshotAsync(test, id);
            await RejectInputsAsync(test, id, version, enabled, direct);
            await RejectTargetsAsync(test, enabled, direct);
            Assert.Equal(before.Digest, (await SnapshotAsync(test, id)).Digest);
            using var accepted = await test.UpdateAsync(id, enabled ? ConfigurationBody : SecurityBody, version, direct: direct, enabled: enabled);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var details = await accepted.Content.ReadFromJsonAsync<ApplicationDetails>(TestContext.Current.CancellationToken);
            Assert.Equal(enabled, details!.Enabled);
            Assert.Equal(created.Application.Credential, details.Credential);
            Assert.Equal(before.NativeDigest, (await SnapshotAsync(test, id)).NativeDigest);
            var stale = version;
            version = accepted.Headers.ETag!.Tag;
            Assert.Equal(ApplicationVersion.Create(id, before.Policy.Revision + 1), version);
            var committed = await SnapshotAsync(test, id);
            var audit = Assert.Single(committed.Audits, row => !before.Audits.Any(prior => prior.Id == row.Id));
            Assert.Equal(enabled ? EnableEvent : DisableEvent, audit.Event);
            Assert.Equal(id, audit.TargetId);
            var reason = Assert.Single(committed.LifecycleAudits, row => !before.LifecycleAudits.Any(prior => prior.AuditId == row.AuditId));
            Assert.Equal(audit.Id, reason.AuditId);
            Assert.Equal(enabled ? 3 : 2, (int)reason.ReasonCode);
            using var repeat = await test.UpdateAsync(id, Body, version, direct: direct, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(repeat, HttpStatusCode.Conflict);
            using var replay = await test.UpdateAsync(id, Body, stale, direct: direct, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(replay, HttpStatusCode.PreconditionFailed);
            Assert.Equal(committed.Digest, (await SnapshotAsync(test, id)).Digest);
        }
    }

    /// <summary>Uses actual native browser proof failures, missing bearer and current persisted permission loss for both lifecycle actions.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineSession_When_LifecycleCsrfOriginBearerOrPermissionIsMissing_Then_AllStateRemainsUnchanged(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var id = created.Application.Id;
        var version = create.Headers.ETag!.Tag;
        var before = await SnapshotAsync(test, id);
        foreach (var enabled in new[] { false, true })
        {
            using var missing = await test.UpdateAsync(id, Body, version, authenticated: false, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(missing, HttpStatusCode.BadRequest);
            using var invalid = await test.UpdateAsync(id, Body, version, csrf: InvalidProof, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(invalid, HttpStatusCode.BadRequest);
            using var foreign = await test.UpdateAsync(id, Body, version, origin: Foreign, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(foreign, HttpStatusCode.BadRequest);
            using var anonymous = await test.UpdateAsync(id, Body, version, direct: true, authenticated: false, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(anonymous, HttpStatusCode.Unauthorized);
        }
        await using var scope = test.Scope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        foreach (var enabled in new[] { false, true })
        {
            foreach (var direct in new[] { false, true })
            {
                using var denied = await test.UpdateAsync(id, Body, version, direct: direct, enabled: enabled);
                await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.Forbidden);
            }
        }
        Assert.Equal(before.Digest, (await SnapshotAsync(test, id)).Digest);
    }

    /// <summary>The original native bearer/continuation expiry, rather than a fabricated extended credential, prevents both real HTTP lifecycle routes.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineNativeDeadline_When_ItExpiresAfterLastSecondDisable_Then_EnableAndDisableRequireNewSignIn(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var create = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var created = await ApplicationHttpTests.CreatedAsync(create);
        var nativeExpiry = await NativeDeadlineAsync(test);
        var earliest = test.AccessExpiresAt < nativeExpiry ? test.AccessExpiresAt : nativeExpiry;
        test.Environment.Issuer.Account.Journey.Clock.Now = earliest.AddSeconds(-1);
        using var disabled = await test.UpdateAsync(created.Application.Id, Body, create.Headers.ETag!.Tag, enabled: false);
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        var proof = await test.TokenAsync();
        var before = await SnapshotAsync(test, created.Application.Id);
        test.Environment.Issuer.Account.Journey.Clock.Now = nativeExpiry;
        foreach (var enabled in new[] { false, true })
        {
            foreach (var direct in new[] { false, true })
            {
                using var denied = await test.UpdateAsync(created.Application.Id, Body, disabled.Headers.ETag!.Tag, direct: direct, csrf: proof, enabled: enabled);
                await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.Unauthorized);
            }
        }
        Assert.Equal(before.Digest, (await SnapshotAsync(test, created.Application.Id)).Digest);
    }

    /// <summary>Demands exact status for body aliases, omitted confirmation and noncanonical preconditions before any valid dispatch.</summary>
    private static async Task RejectInputsAsync(ApplicationHttpFixture test, string id, string version, bool enabled, bool direct)
    {
        foreach (var body in InvalidBodies)
        {
            using var denied = await test.UpdateAsync(id, body, version, direct: direct, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(denied, HttpStatusCode.BadRequest);
        }
        foreach (var tag in new[] { null, Wildcard, Weak + version, version + Comma + version, ApplicationVersion.Create(Missing, 1) })
        {
            using var denied = await test.UpdateAsync(id, Body, tag, direct: direct, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(denied, tag is null ? HttpStatusCode.PreconditionRequired : HttpStatusCode.PreconditionFailed);
        }
    }

    /// <summary>Independently verifies the actual protected native registration remains unchanged, and missing targets cannot be adopted.</summary>
    private static async Task RejectTargetsAsync(ApplicationHttpFixture test, bool enabled, bool direct)
    {
        await using var scope = test.Scope();
        var system = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorProtocolProfile>()
            .Select(row => row.ApplicationId).SingleAsync(TestContext.Current.CancellationToken);
        var before = await NativeDigestAsync(test, system);
        foreach (var target in new[] { system, Missing })
        {
            using var denied = await test.UpdateAsync(target, Body, ApplicationVersion.Create(target, 1), direct: direct, enabled: enabled);
            await ApplicationHttpTests.ProblemAsync(denied, target == system ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound);
        }
        Assert.Equal(before, await NativeDigestAsync(test, system));
    }
}
