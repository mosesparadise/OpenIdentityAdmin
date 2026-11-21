using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Exercises credentialed and public profile rows against real database checks without creating an issuer registration.</summary>
internal static class ClientProfileStorageChecks
{
    private static readonly string[] PublicProfiles = ["managed-native-v1", "managed-browser-v1", "managed-device-v1"];
    private static readonly string[] CredentialedProfiles = ["managed-web-v1", "managed-web-key-v1", "managed-machine-v1", "managed-machine-key-v1"];

    private const string Padded = "managed-web-v1 ";
    private const string WrongCase = "managed-Web-v1";

    /// <summary>Checks persistence and fresh reads as well as missing, unexpected and partially populated credential metadata.</summary>
    internal static async Task RequireAsync(ManagedSchemaFixture fixture)
    {
        await RequireWriteAsync(fixture, Padded, 1, ManagedSchemaRows.Created, ManagedSchemaRows.Expires, accepted: false);
        await RequireWriteAsync(fixture, WrongCase, 1, ManagedSchemaRows.Created, ManagedSchemaRows.Expires, accepted: false);
        foreach (var profile in PublicProfiles)
        {
            await RequireWriteAsync(fixture, profile, 0, null, null, accepted: true);
            await RequireWriteAsync(fixture, profile, 0, null, null, accepted: false, aggregateRevision: 0);
            await RequireWriteAsync(fixture, profile, 0, null, null, accepted: false, observedAt: -1);
            await RequireWriteAsync(fixture, profile, 1, null, null, accepted: false);
            await RequireWriteAsync(fixture, profile, 0, ManagedSchemaRows.Created, null, accepted: false);
            await RequireWriteAsync(fixture, profile, 0, null, ManagedSchemaRows.Expires, accepted: false);
        }
        foreach (var profile in CredentialedProfiles)
        {
            await RequireWriteAsync(fixture, profile, 1, ManagedSchemaRows.Created, ManagedSchemaRows.Expires, accepted: true);
            await RequireWriteAsync(fixture, profile, 0, ManagedSchemaRows.Created, ManagedSchemaRows.Expires, accepted: false);
            await RequireWriteAsync(fixture, profile, 1, null, ManagedSchemaRows.Expires, accepted: false);
            await RequireWriteAsync(fixture, profile, 1, ManagedSchemaRows.Created, null, accepted: false);
        }
    }

    /// <summary>Uses a unique synthetic ID and a fresh context so earlier accepted or rejected rows cannot mask a constraint failure.</summary>
    private static async Task RequireWriteAsync(ManagedSchemaFixture fixture, string profile, long revision, long? issued, long? expires,
        bool accepted, long aggregateRevision = 1, long observedAt = ManagedSchemaRows.Expires)
    {
        await using var scope = fixture.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = ManagedSchemaRows.Policy();
        row.ApplicationId = ManagedSchemaRows.Digest();
        row.Profile = profile;
        row.Revision = aggregateRevision;
        row.ObservedAt = observedAt;
        row.CredentialRevision = revision;
        row.CredentialIssuedAt = issued;
        row.CredentialExpiresAt = expires;
        database.Add(row);
        if (!accepted)
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(fixture.Token));
            return;
        }
        await database.SaveChangesAsync(fixture.Token);
        var actual = await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(value => value.ApplicationId == row.ApplicationId, fixture.Token);
        Assert.Equal((profile, revision, issued, expires), (actual.Profile, actual.CredentialRevision, actual.CredentialIssuedAt, actual.CredentialExpiresAt));
    }
}
