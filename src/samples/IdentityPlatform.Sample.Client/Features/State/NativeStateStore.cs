using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Sample.Client.Features.State;

/// <summary>Adapts the bounded disposable ledger to native OpenIddict managers without replacing protocol or replay decisions.</summary>
internal sealed class NativeStateStore(StateLedger ledger) : IOpenIddictTokenStore<StateRecord>
{
    /// <summary>Reads detached native applicationid data after honoring cancellation.</summary>
    public ValueTask<string?> GetApplicationIdAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.ApplicationId, cancellationToken);

    /// <summary>Changes only the caller-owned applicationid value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetApplicationIdAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.ApplicationId = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native authorizationid data after honoring cancellation.</summary>
    public ValueTask<string?> GetAuthorizationIdAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.AuthorizationId, cancellationToken);

    /// <summary>Changes only the caller-owned authorizationid value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetAuthorizationIdAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.AuthorizationId = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native creationdate data after honoring cancellation.</summary>
    public ValueTask<DateTimeOffset?> GetCreationDateAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.CreationDate, cancellationToken);

    /// <summary>Changes only the caller-owned creationdate value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetCreationDateAsync(StateRecord token, DateTimeOffset? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.CreationDate = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native expirationdate data after honoring cancellation.</summary>
    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.ExpirationDate, cancellationToken);

    /// <summary>Changes only the caller-owned expirationdate value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetExpirationDateAsync(StateRecord token, DateTimeOffset? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.ExpirationDate = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native id data after honoring cancellation.</summary>
    public ValueTask<string?> GetIdAsync(StateRecord token, CancellationToken cancellationToken) => Result<string?>(token.Id, cancellationToken);

    /// <summary>Reads detached native payload data after honoring cancellation.</summary>
    public ValueTask<string?> GetPayloadAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.Payload, cancellationToken);

    /// <summary>Changes only the caller-owned payload value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetPayloadAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.Payload = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native properties data after honoring cancellation.</summary>
    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.Properties.ToImmutableDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal), cancellationToken);

    /// <summary>Changes only the caller-owned properties value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetPropertiesAsync(StateRecord token, ImmutableDictionary<string, JsonElement> value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.Properties = value.ToImmutableDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal);
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native redemptiondate data after honoring cancellation.</summary>
    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.RedemptionDate, cancellationToken);

    /// <summary>Changes only the caller-owned redemptiondate value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetRedemptionDateAsync(StateRecord token, DateTimeOffset? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.RedemptionDate = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native referenceid data after honoring cancellation.</summary>
    public ValueTask<string?> GetReferenceIdAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.ReferenceId, cancellationToken);

    /// <summary>Changes only the caller-owned referenceid value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetReferenceIdAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.ReferenceId = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native status data after honoring cancellation.</summary>
    public ValueTask<string?> GetStatusAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.Status, cancellationToken);

    /// <summary>Changes only the caller-owned status value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetStatusAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.Status = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native subject data after honoring cancellation.</summary>
    public ValueTask<string?> GetSubjectAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.Subject, cancellationToken);

    /// <summary>Changes only the caller-owned subject value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetSubjectAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.Subject = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Reads detached native type data after honoring cancellation.</summary>
    public ValueTask<string?> GetTypeAsync(StateRecord token, CancellationToken cancellationToken) => Result(token.Type, cancellationToken);

    /// <summary>Changes only the caller-owned type value; persistence still requires a versioned ledger update.</summary>
    public ValueTask SetTypeAsync(StateRecord token, string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        token.Type = value;
        return ValueTask.CompletedTask;
    }

    /// <summary>Creates an unpersisted native state entity without authority.</summary>
    public ValueTask<StateRecord> InstantiateAsync(CancellationToken cancellationToken) => Result(new StateRecord(), cancellationToken);

    /// <summary>Atomically admits a bounded detached native state record.</summary>
    public ValueTask CreateAsync(StateRecord token, CancellationToken cancellationToken)
    {
        ledger.Create(token, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <summary>Applies native consumption changes using the originally observed concurrency version.</summary>
    public ValueTask UpdateAsync(StateRecord token, CancellationToken cancellationToken)
    {
        ledger.Update(token, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <summary>Deletes only the observed version; stale deletion cannot remove a newer consumption outcome.</summary>
    public ValueTask DeleteAsync(StateRecord token, CancellationToken cancellationToken)
    {
        ledger.Delete(token, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <summary>Counts all retained state, including terminal records.</summary>
    public ValueTask<long> CountAsync(CancellationToken cancellationToken) => Result(ledger.Snapshot(cancellationToken).LongLength, cancellationToken);

    /// <summary>Evaluates a trusted native query over detached bounded snapshots.</summary>
    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<StateRecord>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        Result(query(ledger.Snapshot(cancellationToken).AsQueryable()).LongCount(), cancellationToken);

    /// <summary>Finds an exact state identity without sharing ledger references.</summary>
    public ValueTask<StateRecord?> FindByIdAsync(string identifier, CancellationToken cancellationToken) =>
        Result(ledger.Snapshot(cancellationToken).SingleOrDefault(record => record.Id == identifier), cancellationToken);

    /// <summary>Looks up the exact native-manager transformed reference identifier.</summary>
    public ValueTask<StateRecord?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken) =>
        Result(ledger.Snapshot(cancellationToken).SingleOrDefault(record => record.ReferenceId == identifier), cancellationToken);

    /// <summary>Filters native state using ordinal identities and optional native criteria.</summary>
    public IAsyncEnumerable<StateRecord> FindAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken) =>
        Enumerate(ledger.Snapshot(cancellationToken).Where(record => Matches(record, subject, client, status, type)), cancellationToken);

    /// <summary>Returns matching detached application-bound state.</summary>
    public IAsyncEnumerable<StateRecord> FindByApplicationIdAsync(string identifier, CancellationToken cancellationToken) =>
        Enumerate(ledger.Snapshot(cancellationToken).Where(record => record.ApplicationId == identifier), cancellationToken);

    /// <summary>Returns matching detached authorization-bound state.</summary>
    public IAsyncEnumerable<StateRecord> FindByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken) =>
        Enumerate(ledger.Snapshot(cancellationToken).Where(record => record.AuthorizationId == identifier), cancellationToken);

    /// <summary>Returns exact subject matches without case folding.</summary>
    public IAsyncEnumerable<StateRecord> FindBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        Enumerate(ledger.Snapshot(cancellationToken).Where(record => record.Subject == subject), cancellationToken);

    /// <summary>Evaluates trusted single-result native queries on detached snapshots.</summary>
    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<StateRecord>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        Result(query(ledger.Snapshot(cancellationToken).AsQueryable(), state).FirstOrDefault(), cancellationToken);

    /// <summary>Provides deterministic bounded state paging to native consumers.</summary>
    public IAsyncEnumerable<StateRecord> ListAsync(int? count, int? offset, CancellationToken cancellationToken) =>
        Enumerate(ledger.Snapshot(cancellationToken).OrderBy(record => record.Id, StringComparer.Ordinal).Skip(offset ?? 0).Take(count ?? StateLedger.Capacity), cancellationToken);

    /// <summary>Evaluates trusted native projections over detached snapshots.</summary>
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<StateRecord>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        Enumerate(query(ledger.Snapshot(cancellationToken).AsQueryable(), state), cancellationToken);

    /// <summary>Prunes expired or sufficiently old terminal state without evicting valid attempts.</summary>
    public ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken) => Result(ledger.Prune(threshold, cancellationToken), cancellationToken);

    /// <summary>Revokes exact native criteria atomically with concurrent redemption.</summary>
    public ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken) =>
        Result(ledger.Revoke(record => Matches(record, subject, client, status, type), cancellationToken), cancellationToken);

    /// <summary>Revokes exact application matches without matching unbound records.</summary>
    public ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken = default) =>
        Result(ledger.Revoke(record => record.ApplicationId == identifier, cancellationToken), cancellationToken);

    /// <summary>Revokes exact authorization matches.</summary>
    public ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken) =>
        Result(ledger.Revoke(record => record.AuthorizationId == identifier, cancellationToken), cancellationToken);

    /// <summary>Revokes exact subject matches.</summary>
    public ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken = default) =>
        Result(ledger.Revoke(record => record.Subject == subject, cancellationToken), cancellationToken);

    /// <summary>Matches only supplied native query predicates.</summary>
    private static bool Matches(StateRecord record, string? subject, string? client, string? status, string? type) =>
        (subject is null || record.Subject == subject) && (client is null || record.ApplicationId == client) &&
        (status is null || record.Status == status) && (type is null || record.Type == type);

    /// <summary>Honors cancellation even for synchronous process-local operations.</summary>
    private static ValueTask<T> Result<T>(T value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(value);
    }

    /// <summary>Checks cancellation during bounded native enumeration without retaining mutable ledger state.</summary>
    private static async IAsyncEnumerable<T> Enumerate<T>(IEnumerable<T> values, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return value;
        }
    }
}
