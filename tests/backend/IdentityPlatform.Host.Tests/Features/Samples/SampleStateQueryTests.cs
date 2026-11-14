using System.Collections.Immutable;
using System.Text.Json;
using IdentityPlatform.Sample.Client.Features.State;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Checks the adapter's native query/revocation contract over detached real ledger snapshots, beyond the sign-in hot path.</summary>
public sealed class SampleStateQueryTests
{
    private const string Subject = "subject";
    private const string OtherSubject = "SUBJECT";
    private const string Application = "application";
    private const string Authorization = "authorization";
    private const string SafeProperty = "safe";
    private const string Property = "property";
    private const string Json = "{\"safe\":true}";
    private const int LifetimeMinutes = 5;

    /// <summary>Exact identities, optional filtering, paging and projected queries cannot mutate persisted state or match case-folded subjects.</summary>
    [Fact]
    public async Task Given_BoundRecords_When_NativeQueriesAndRevocationRun_Then_OnlyExactDetachedMatchesChange()
    {
        var clock = new SampleClock();
        var store = new NativeStateStore(new StateLedger(clock));
        var first = await CreateAsync(store, clock, Subject, Application, Authorization);
        var second = await CreateAsync(store, clock, OtherSubject, null, null);
        Assert.Single(await store.FindByApplicationIdAsync(Application, TestContext.Current.CancellationToken).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await store.FindByAuthorizationIdAsync(Authorization, TestContext.Current.CancellationToken).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await store.FindBySubjectAsync(Subject, TestContext.Current.CancellationToken).ToArrayAsync(TestContext.Current.CancellationToken));
        var matches = await store.FindAsync(Subject, Application, OpenIddictConstants.Statuses.Valid, OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, TestContext.Current.CancellationToken).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(first.Id, Assert.Single(matches).Id);
        Assert.Single(await store.ListAsync(1, 1, TestContext.Current.CancellationToken).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(first.Id, await store.GetAsync((rows, id) => rows.Where(row => row.Id == id).Select(row => row.Id), first.Id, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { second.Id }, await store.ListAsync((rows, subject) => rows.Where(row => row.Subject == subject).Select(row => row.Id), OtherSubject, TestContext.Current.CancellationToken).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.RevokeByApplicationIdAsync(Application, TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.RevokeByAuthorizationIdAsync(Authorization, TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.RevokeBySubjectAsync(OtherSubject, TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await store.FindByIdAsync(first.Id, TestContext.Current.CancellationToken))!.Status);
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await store.FindByIdAsync(second.Id, TestContext.Current.CancellationToken))!.Status);
    }

    /// <summary>Sets native properties through the adapter and retains cloned JSON after the caller disposes the document.</summary>
    private static async Task<StateRecord> CreateAsync(NativeStateStore store, TimeProvider clock, string subject, string? application, string? authorization)
    {
        var token = TestContext.Current.CancellationToken;
        var record = await store.InstantiateAsync(token);
        await store.SetSubjectAsync(record, subject, token);
        await store.SetApplicationIdAsync(record, application, token);
        await store.SetAuthorizationIdAsync(record, authorization, token);
        await store.SetTypeAsync(record, OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken, token);
        await store.SetStatusAsync(record, OpenIddictConstants.Statuses.Valid, token);
        await store.SetCreationDateAsync(record, clock.GetUtcNow(), token);
        await store.SetExpirationDateAsync(record, clock.GetUtcNow().AddMinutes(LifetimeMinutes), token);
        using var document = JsonDocument.Parse(Json);
        await store.SetPropertiesAsync(record, ImmutableDictionary<string, JsonElement>.Empty.Add(Property, document.RootElement), token);
        await store.CreateAsync(record, token);
        Assert.True((await store.GetPropertiesAsync(record, token))[Property].GetProperty(SafeProperty).GetBoolean());
        return record;
    }
}
