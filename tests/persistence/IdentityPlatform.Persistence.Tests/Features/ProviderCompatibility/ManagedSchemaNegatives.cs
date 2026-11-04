using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises relational checks with separately authored malformed rows; a fresh scope isolates every rejected write.</summary>
internal static class ManagedSchemaNegatives
{
    private const string UnknownProfile = "unsupported";
    private const int UnknownState = 8;
    private const int AccountOverflow = 451;
    private const int StampOverflow = 257;
    private static readonly Action<ApplicationPolicy>[] InvalidPolicies =
    [
        row => row.Revision = 0, row => row.CredentialRevision = 0, row => row.CredentialRevision = row.Revision + 1,
        row => row.CredentialIssuedAt = -1, row => row.CredentialExpiresAt = row.CredentialIssuedAt,
        row => row.ObservedAt = row.CredentialIssuedAt - 1, row => row.Profile = UnknownProfile
    ];
    private static readonly Action<ManagedLoginInteraction>[] InvalidInteractions =
    [
        row => row.Revision = 0, row => row.ApplicationRevision = 0, row => row.CreatedAt = -1,
        row => row.ExpiresAt = row.CreatedAt, row => row.ExpiresAt++, row => row.ObservedAt = row.CreatedAt - 1,
        row => row.RetainUntil = row.ExpiresAt - 1, row => row.RequestId = null, row => row.RequestExpiresAt = null,
        row => row.NativeDependencyCreatedAt = null, row => row.NativeDependencyExpiresAt = null,
        row => row.RequestExpiresAt = row.ExpiresAt - 1, row => row.NativeDependencyExpiresAt = row.RequestExpiresAt - 1,
        row => row.State = (ManagedLoginState)UnknownState, row => row.CeremonyId = null, row => row.CeremonyBrowserDigest = null,
        row => row.BrowserBudgetId = null, row => row.UserId = null, row => row.SecurityStamp = null,
        row => row.AuthenticationGeneration = null, row => row.AuthenticationGeneration = 0,
        row => row.FactorGeneration = null, row => row.FactorGeneration = 0, row => row.EnrollmentRevision = null,
        row => row.EnrollmentRevision = -1,
        row => row.PasswordVerifiedAt = null, row => row.PasswordVerifiedAt = row.CreatedAt - 1,
        row => row.MfaVerifiedAt = null, row => row.MfaVerifiedAt = row.PasswordVerifiedAt - 1,
        row => row.ConsentVerifiedAt = null, row => row.ConsentVerifiedAt = row.MfaVerifiedAt - 1,
        row => row.ConsentVerifiedAt = row.ExpiresAt, row => row.UserId = new string('u', AccountOverflow),
        row => row.SecurityStamp = new string('s', StampOverflow), row => row.State = ManagedLoginState.Consumed,
        row => row.CodeId = ManagedSchemaRows.Digest()
    ];

    /// <summary>Rejects invalid static invariants, then exercises nullable uniqueness and nonnull binding collisions independently.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture)
    {
        foreach (var mutate in InvalidPolicies)
        {
            var row = ManagedSchemaRows.Policy();
            row.ApplicationId = ManagedSchemaRows.Digest();
            mutate(row);
            await RejectAsync(fixture, row);
        }
        foreach (var mutate in InvalidInteractions)
        {
            var row = ManagedSchemaRows.Interaction(ManagedLoginState.Approved);
            mutate(row);
            await RejectAsync(fixture, row);
        }
        await RejectPrematureProofAsync(fixture);
        await DuplicateAsync(fixture, row => row.RequestId);
        await DuplicateAsync(fixture, row => row.CodeId);
        await DuplicateAsync(fixture, row => row.AuthorizationId);
        await using var scope = fixture.Scope();
        var existing = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ManagedLoginInteraction>().AsNoTracking()
            .SingleAsync(row => row.State == ManagedLoginState.Consumed, fixture.Token);
        var duplicate = ManagedSchemaRows.Interaction(ManagedLoginState.Approved);
        duplicate.CeremonyId = existing.CeremonyId;
        await RejectAsync(fixture, duplicate);
    }

    /// <summary>Each earlier phase rejects a proof that belongs to a later phase while all other columns remain valid.</summary>
    private static async Task RejectPrematureProofAsync(ManagedSchemaFixture fixture)
    {
        var password = ManagedSchemaRows.Interaction(ManagedLoginState.PendingPassword);
        password.CeremonyId = Guid.NewGuid();
        await RejectAsync(fixture, password);
        var mfa = ManagedSchemaRows.Interaction(ManagedLoginState.PendingMfa);
        mfa.MfaVerifiedAt = mfa.PasswordVerifiedAt;
        await RejectAsync(fixture, mfa);
        var consent = ManagedSchemaRows.Interaction(ManagedLoginState.PendingConsent);
        consent.ConsentVerifiedAt = consent.MfaVerifiedAt;
        await RejectAsync(fixture, consent);
    }

    /// <summary>Attempts an actual fresh provider write and requires a relational update failure rather than a validation shortcut.</summary>
    private static async Task RejectAsync(ManagedSchemaFixture fixture, object row)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Add(row);
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
    }

    /// <summary>Copies exactly one persisted native binding into an otherwise distinct valid consumed row to exercise its unique index.</summary>
    private static async Task DuplicateAsync(ManagedSchemaFixture fixture, System.Linq.Expressions.Expression<Func<ManagedLoginInteraction, string?>> field)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var existing = await database.Set<ManagedLoginInteraction>().AsNoTracking().Where(row => row.State == ManagedLoginState.Consumed)
            .Select(field).SingleAsync(fixture.Token);
        var row = ManagedSchemaRows.Interaction(ManagedLoginState.Consumed);
        var property = (System.Reflection.PropertyInfo)((System.Linq.Expressions.MemberExpression)field.Body).Member;
        property.SetValue(row, existing);
        await RejectAsync(fixture, row);
    }
}
