using System.Collections.Immutable;
using System.Text.Json;
using IdentityPlatform.Sample.Client.Features.State;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Exercises disposable native state ownership, bounded retention and real manager redemption semantics.</summary>
public sealed class SampleStateTests
{
    private const string ValueOriginalText = "{\"value\":\"original\"}";
    private const string TestText = "test";
    private const string CallerMutationText = "caller mutation";
    private const string ReadMutationText = "read mutation";
    private const string ValueText = "value";
    private const int LifetimeMinutes = 5;
    private const string SyntheticNativeReferenceText = "synthetic-native-reference";
    private const int ExpectedCapacity = 128;
    private const int ExpiredMinutes = 2;
    private const string NativeTransformedReferenceText = "native-transformed-reference";
    private const string DifferentText = "different";
    private const string PayloadText = "payload";
    private const int OversizedPayloadLength = 32769;
    private const string PropertiesText = "properties";
    private const string LargeText = "large";
    private const int OversizedPropertyLength = 8193;
    private const string LifetimeText = "lifetime";
    private const int ExcessiveLifetimeMinutes = 6;
    private const string FutureText = "future";
    private const string MissingExpiryText = "missing-expiry";
    private const string TypeText = "type";
    private const string StatusField = "status";
    private const string UnknownText = "unknown";
    private const int ExpectedPayloadLimit = 32768;
    private const string XText = "x";
    private const int MaximumPropertyValueLength = 8184;
    private const string FirstNativeBindingText = "first-native-binding";
    private const string ReboundText = "rebound";
    private const string SubjectOneText = "subject-one";
    private const string SubjectTwoText = "subject-two";
    private const string ProtectedSyntheticStateText = "protected synthetic state";

    /// <summary>A returned mutable object or disposed source document must not change or invalidate retained state.</summary>
    [Fact]
    public async Task Given_DetachedState_When_CallersMutateCopies_Then_PersistedValuesRemainUnchanged()
    {
        var clock = new SampleClock();
        var store = new NativeStateStore(new StateLedger(clock));
        var record = Record(clock);
        using (var document = JsonDocument.Parse(ValueOriginalText))
        {
            record.Properties = ImmutableDictionary<string, JsonElement>.Empty.Add(TestText, document.RootElement);
            await store.CreateAsync(record, TestContext.Current.CancellationToken);
        }
        record.Payload = CallerMutationText;
        var first = await store.FindByIdAsync(record.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        first.Payload = ReadMutationText;
        var second = await store.FindByIdAsync(record.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(second);
        Assert.Equal("protected synthetic state", second.Payload);
        Assert.Equal("original", second.Properties[TestText].GetProperty(ValueText).GetString());
    }

    /// <summary>Two actual native managers consuming detached views cannot both redeem one reference state.</summary>
    [Fact]
    [Trait("CriticalDecision", "sample-state-cas")]
    public async Task Given_OneNativeState_When_ConcurrentManagersRedeem_Then_ExactlyOneSucceeds()
    {
        var clock = new SampleClock();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(clock).AddSingleton<StateLedger>();
        services.AddOpenIddict().AddCore(options => options.SetDefaultTokenEntity<StateRecord>().ReplaceTokenStore<StateRecord, NativeStateStore>().DisableEntityCaching());
        await using var provider = services.BuildServiceProvider();
        await using var one = provider.CreateAsyncScope();
        await using var two = provider.CreateAsyncScope();
        var firstManager = one.ServiceProvider.GetRequiredService<OpenIddictTokenManager<StateRecord>>();
        var secondManager = two.ServiceProvider.GetRequiredService<OpenIddictTokenManager<StateRecord>>();
        var original = await firstManager.CreateAsync(new OpenIddictTokenDescriptor
        {
            CreationDate = clock.GetUtcNow(),
            ExpirationDate = clock.GetUtcNow().AddMinutes(LifetimeMinutes),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken,
            ReferenceId = SyntheticNativeReferenceText
        }, TestContext.Current.CancellationToken);
        Assert.NotNull(original);
        var copy = await secondManager.FindByReferenceIdAsync(SyntheticNativeReferenceText, TestContext.Current.CancellationToken);
        Assert.NotNull(copy);
        Assert.Equal(original.Id, copy.Id);
        Assert.NotEqual(SyntheticNativeReferenceText, copy.ReferenceId);
        var results = await Task.WhenAll(Task.Run(async () => await firstManager.TryRedeemAsync(original, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            Task.Run(async () => await secondManager.TryRedeemAsync(copy, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
        if (results.All(result => result))
        {
            const string marker = "sample-state-cas-bypass";
            var redeemed = await firstManager.FindByIdAsync(original.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(redeemed);
            Assert.True(await firstManager.HasStatusAsync(redeemed, OpenIddictConstants.Statuses.Redeemed, TestContext.Current.CancellationToken));
            Assert.Fail(marker);
        }
        Assert.Single(results, result => result);
        var persisted = await firstManager.FindByIdAsync(original.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.True(await firstManager.HasStatusAsync(persisted, OpenIddictConstants.Statuses.Redeemed, TestContext.Current.CancellationToken));
    }

    /// <summary>Native CAS rejects stale update and deletion, and terminal state never becomes valid again.</summary>
    [Fact]
    public void Given_RedeemedState_When_StaleOrRevivingMutationOccurs_Then_NativeConcurrencyFailurePreservesOutcome()
    {
        var clock = new SampleClock();
        var ledger = new StateLedger(clock);
        var record = Record(clock);
        ledger.Create(record, TestContext.Current.CancellationToken);
        var stale = ledger.Snapshot(TestContext.Current.CancellationToken).Single();
        record.Status = OpenIddictConstants.Statuses.Redeemed;
        ledger.Update(record, TestContext.Current.CancellationToken);
        Assert.Throws<OpenIddictExceptions.ConcurrencyException>(() => ledger.Update(stale, TestContext.Current.CancellationToken));
        Assert.Throws<OpenIddictExceptions.ConcurrencyException>(() => ledger.Delete(stale, TestContext.Current.CancellationToken));
        record.Status = OpenIddictConstants.Statuses.Valid;
        Assert.Throws<OpenIddictExceptions.ConcurrencyException>(() => ledger.Update(record, TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.Statuses.Redeemed, ledger.Snapshot(TestContext.Current.CancellationToken).Single().Status);
    }

    /// <summary>Total capacity includes terminal records; live attempts are never evicted to admit more work.</summary>
    [Fact]
    public void Given_FullRetainedCapacity_When_AdmissionContinues_Then_RejectsUntilExpiryWithoutLiveEviction()
    {
        var clock = new SampleClock();
        var ledger = new StateLedger(clock);
        for (var index = 0; index < ExpectedCapacity; index++)
        {
            var record = Record(clock);
            ledger.Create(record, TestContext.Current.CancellationToken);
            if (index % ExpiredMinutes == 0)
            {
                record.Status = OpenIddictConstants.Statuses.Redeemed;
                ledger.Update(record, TestContext.Current.CancellationToken);
            }
        }
        var before = ledger.Snapshot(TestContext.Current.CancellationToken).Select(record => (record.Id, record.Version, record.Status)).ToArray();
        Assert.Throws<InvalidOperationException>(() => ledger.Create(Record(clock), TestContext.Current.CancellationToken));
        Assert.Equal(before, ledger.Snapshot(TestContext.Current.CancellationToken).Select(record => (record.Id, record.Version, record.Status)).ToArray());
        Assert.Equal(128, before.Length);
        clock.Now = clock.Now.AddMinutes(LifetimeMinutes);
        ledger.Create(Record(clock), TestContext.Current.CancellationToken);
        Assert.Single(ledger.Snapshot(TestContext.Current.CancellationToken));
    }

    /// <summary>The manager-transformed reference identity is unique even when a second entity has another primary ID.</summary>
    [Fact]
    public async Task Given_ExistingReference_When_DuplicateIsCreated_Then_OriginalRemainsFindable()
    {
        var clock = new SampleClock();
        var store = new NativeStateStore(new StateLedger(clock));
        var first = Record(clock);
        first.ReferenceId = NativeTransformedReferenceText;
        await store.CreateAsync(first, TestContext.Current.CancellationToken);
        var other = Record(clock);
        other.ReferenceId = first.ReferenceId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateAsync(other, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(first.Id, (await store.FindByReferenceIdAsync(first.ReferenceId, TestContext.Current.CancellationToken))?.Id);
        Assert.Null(await store.FindByReferenceIdAsync(DifferentText, TestContext.Current.CancellationToken));
    }

    /// <summary>State size and lifetime limits reject malformed snapshots without retaining partial data.</summary>
    [Theory]
    [InlineData("payload")]
    [InlineData("properties")]
    [InlineData("lifetime")]
    [InlineData("future")]
    [InlineData("missing-expiry")]
    [InlineData("type")]
    [InlineData("status")]
    public void Given_InvalidNativeSnapshot_When_AdmissionRuns_Then_NoStateIsRetained(string defect)
    {
        var clock = new SampleClock();
        var ledger = new StateLedger(clock);
        var record = Record(clock);
        switch (defect)
        {
            case PayloadText:
                record.Payload = new string('x', OversizedPayloadLength);
                break;
            case PropertiesText:
                record.Properties = ImmutableDictionary<string, JsonElement>.Empty.Add(LargeText, JsonSerializer.SerializeToElement(new string('x', OversizedPropertyLength)));
                break;
            case LifetimeText:
                record.ExpirationDate = clock.Now.AddMinutes(ExcessiveLifetimeMinutes);
                break;
            case FutureText:
                record.CreationDate = clock.Now.AddSeconds(1);
                break;
            case MissingExpiryText:
                record.ExpirationDate = null;
                break;
            case TypeText:
                record.Type = OpenIddictConstants.TokenTypeHints.AccessToken;
                break;
            case StatusField:
                record.Status = UnknownText;
                break;
        }
        Assert.Throws<InvalidOperationException>(() => ledger.Create(record, TestContext.Current.CancellationToken));
        Assert.Empty(ledger.Snapshot(TestContext.Current.CancellationToken));
    }

    /// <summary>Exact independent byte/character limits remain usable while identity rebinding after attachment is rejected.</summary>
    [Fact]
    public void Given_ExactStateBounds_When_IdentityIsRebound_Then_InitialAdmissionSucceedsAndUpdateFails()
    {
        var clock = new SampleClock();
        var ledger = new StateLedger(clock);
        var record = Record(clock);
        record.Payload = new string('x', ExpectedPayloadLimit);
        record.Properties = ImmutableDictionary<string, JsonElement>.Empty.Add(XText, JsonSerializer.SerializeToElement(new string('x', MaximumPropertyValueLength)));
        Assert.Equal(8192, JsonSerializer.SerializeToUtf8Bytes(record.Properties).Length);
        ledger.Create(record, TestContext.Current.CancellationToken);
        record.ReferenceId = FirstNativeBindingText;
        ledger.Update(record, TestContext.Current.CancellationToken);
        record.ReferenceId = ReboundText;
        Assert.Throws<OpenIddictExceptions.ConcurrencyException>(() => ledger.Update(record, TestContext.Current.CancellationToken));
        Assert.Equal("first-native-binding", ledger.Snapshot(TestContext.Current.CancellationToken).Single().ReferenceId);
    }

    /// <summary>Cancellation before mutation and after enumeration creation cannot produce a state change or result.</summary>
    [Fact]
    public async Task Given_CancelledOperation_When_CreateOrEnumerationRuns_Then_NoMutationOrYieldOccurs()
    {
        var clock = new SampleClock();
        var store = new NativeStateStore(new StateLedger(clock));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var record = Record(clock);
        await store.CreateAsync(record, TestContext.Current.CancellationToken);
        var values = store.ListAsync(null, null, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync(Record(clock), cancellation.Token).AsTask());
        await using var iterator = values.GetAsyncEnumerator(cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => iterator.MoveNextAsync().AsTask());
        Assert.Equal(1, await store.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Pruning old terminal entries preserves unexpired valid records; revoked copies cannot update afterward.</summary>
    [Fact]
    public async Task Given_SubjectRevocation_When_PruningRuns_Then_OnlyOldTerminalRecordsDisappear()
    {
        var clock = new SampleClock();
        var store = new NativeStateStore(new StateLedger(clock));
        var revoked = Record(clock);
        revoked.Subject = SubjectOneText;
        await store.CreateAsync(revoked, TestContext.Current.CancellationToken);
        var retained = Record(clock);
        retained.Subject = SubjectTwoText;
        await store.CreateAsync(retained, TestContext.Current.CancellationToken);
        Assert.Equal(1, await store.RevokeBySubjectAsync(SubjectOneText, TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.PruneAsync(clock.Now.AddSeconds(1), TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindByIdAsync(retained.Id, TestContext.Current.CancellationToken));
        Assert.Null(await store.FindByIdAsync(revoked.Id, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(() => store.UpdateAsync(revoked, TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Creates a synthetic native record with independently specified five-minute bounds.</summary>
    private static StateRecord Record(TimeProvider clock) => new()
    {
        CreationDate = clock.GetUtcNow(),
        ExpirationDate = clock.GetUtcNow().AddMinutes(LifetimeMinutes),
        Status = OpenIddictConstants.Statuses.Valid,
        Type = OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken,
        Payload = ProtectedSyntheticStateText
    };
}

/// <summary>Provides explicit deterministic time for sample lifecycle boundaries.</summary>
internal sealed class SampleClock : TimeProvider
{
    private const int Year = 2026;
    private const int Month = 11;
    private const int Day = 14;
    private const int Hour = 12;

    internal DateTimeOffset Now { get; set; } = new(Year, Month, Day, Hour, 0, 0, TimeSpan.Zero);

    /// <summary>Returns the controlled test clock without wall-time sleeps.</summary>
    public override DateTimeOffset GetUtcNow() => Now;
}
