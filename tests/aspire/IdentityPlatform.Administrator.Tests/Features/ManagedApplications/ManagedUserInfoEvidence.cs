using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Requires exact portable lock-counter changes while preserving every captured authority, credential, consent and audit field.</summary>
internal sealed class ManagedUserInfoEvidence(JsonObject before, string browser, string user)
{
    private const int GovernanceLocksPerCommit = 5;
    private const int MaximumObservedCommits = 3;
    private const string HumanField = "Human";
    private const string GovernanceField = "Governance";
    private const string AdmissionField = "Admission";
    private const string BrowsersField = "Browsers";
    private const string AuthenticationField = "Authentication";
    private const string IdentifierField = "Id";
    private const string UserField = "UserId";
    private const string RevisionField = "Revision";

    /// <summary>Captures detached current rows before the request; expected changes are applied only to a clone of this original state.</summary>
    internal static async Task<ManagedUserInfoEvidence> CaptureAsync(ManagedCacheTestContext test)
    {
        var interaction = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.NotNull(interaction.BrowserBudgetId);
        Assert.NotNull(interaction.UserId);
        return new(await ReadAsync(test), interaction.BrowserBudgetId, interaction.UserId);
    }

    /// <summary>Checks exact commit effects without subtracting actual changes or ignoring revision/authority columns.</summary>
    internal async Task RequireAsync(ManagedCacheTestContext test, int commits)
    {
        Assert.InRange(commits, 0, MaximumObservedCommits);
        var expected = Assert.IsType<JsonObject>(before.DeepClone());
        var human = Assert.IsType<JsonObject>(expected[HumanField]);
        Advance(expected[GovernanceField], IdentifierField, JsonValue.Create(1)!, GovernanceLocksPerCommit * commits);
        Advance(expected[AdmissionField], IdentifierField, JsonValue.Create(1)!, commits);
        Advance(human[BrowsersField], IdentifierField, JsonValue.Create(browser)!, commits);
        Advance(human[AuthenticationField], UserField, JsonValue.Create(user)!, commits);
        Assert.Equal(FactorBinding.Digest(expected), FactorBinding.Digest(await ReadAsync(test)));
    }

    /// <summary>Identifies exactly one original lock row and adds the independently expected increment with overflow checking.</summary>
    private static void Advance(JsonNode? rows, string identity, JsonNode value, int increment)
    {
        var row = Assert.Single(Assert.IsType<JsonArray>(rows), node => JsonNode.DeepEquals(node![identity], value))!;
        row[RevisionField] = checked(row[RevisionField]!.GetValue<long>() + increment);
    }

    /// <summary>Includes the original full human/native/management snapshot plus all coordination rows and current account/enrollment state.</summary>
    private static async Task<JsonObject> ReadAsync(ManagedCacheTestContext test)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(new
        {
            Human = await ManagedHumanEvidence.ReadAsync(test),
            Authority = await ManagedHumanEvidence.AuthorityAsync(test),
            Policies = await database.Set<ApplicationPolicy>().AsNoTracking().OrderBy(row => row.ApplicationId).ToArrayAsync(token),
            Native = await ManagedJourneyEvidence.NativeAsync(database),
            Governance = await database.Set<AdministratorGovernance>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Admission = await database.Set<AdministratorAdmission>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Users = await database.Users.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().OrderBy(row => row.UserId).ToArrayAsync(token)
        }));
    }
}
