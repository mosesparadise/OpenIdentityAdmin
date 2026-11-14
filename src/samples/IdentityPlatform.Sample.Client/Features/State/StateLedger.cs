using System.Text.Json;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Sample.Client.Features.State;

/// <summary>Bounds disposable native state and serializes uniqueness, versioning and terminal transitions within one process.</summary>
internal sealed class StateLedger(TimeProvider clock)
{
    private const int BindingLimit = 128;
    private const int ReferenceLimit = 256;
    private const int IdentifierLength = 32;

    internal const int Capacity = 128;
    internal const int PayloadLimit = 32768;
    internal const int PropertyLimit = 8192;
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const string Rejected = "Sample state storage rejected the operation.";
    private readonly Lock gate = new();
    private readonly Dictionary<string, StateRecord> records = new(StringComparer.Ordinal);

    /// <summary>Returns detached snapshots under the gate; query delegates cannot access retained mutable data.</summary>
    internal StateRecord[] Snapshot(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return records.Values.Select(record => record.Copy()).ToArray();
        }
    }

    /// <summary>Prunes expired data before admission and atomically rejects duplicate identities or exhausted total capacity.</summary>
    internal void Create(StateRecord record, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PruneCore(clock.GetUtcNow(), expiredOnly: true);
            Validate(record);
            if (records.Count >= Capacity || records.ContainsKey(record.Id) || record.Version != 0)
            {
                throw new InvalidOperationException(Rejected);
            }
            UniqueReference(record);
            record.Version = 1;
            records.Add(record.Id, record.Copy());
        }
    }

    /// <summary>Applies a detached native update only at its current version, preserving immutable lifetime and terminal status.</summary>
    internal void Update(StateRecord record, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = RequireVersion(record);
            Validate(record);
            if (record.CreationDate != current.CreationDate || record.ExpirationDate != current.ExpirationDate ||
                record.Type != current.Type || record.Subject != current.Subject || record.ApplicationId != current.ApplicationId ||
                record.AuthorizationId != current.AuthorizationId ||
                current.ReferenceId is not null && record.ReferenceId != current.ReferenceId ||
                current.Payload is not null && record.Payload != current.Payload ||
                current.Status != OpenIddictConstants.Statuses.Valid && record.Status != current.Status)
            {
                throw new OpenIddictExceptions.ConcurrencyException(Rejected);
            }
            UniqueReference(record);
            record.Version = checked(current.Version + 1);
            records[record.Id] = record.Copy();
        }
    }

    /// <summary>Deletes only the version observed by the native manager; a missing record cannot be recreated.</summary>
    internal void Delete(StateRecord record, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = RequireVersion(record);
            records.Remove(record.Id);
        }
    }

    /// <summary>Removes expired records and old terminal records without evicting any unexpired valid attempt.</summary>
    internal long Prune(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return PruneCore(threshold, expiredOnly: false);
        }
    }

    /// <summary>Marks selected records revoked under the same version gate used by native consumption.</summary>
    internal long Revoke(Func<StateRecord, bool> predicate, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matches = records.Values.Where(record => predicate(record.Copy())).ToArray();
            foreach (var record in matches)
            {
                record.Status = OpenIddictConstants.Statuses.Revoked;
                record.Version = checked(record.Version + 1);
            }
            return matches.LongLength;
        }
    }

    /// <summary>Rejects unknown or stale identities using the native manager's recognized concurrency exception.</summary>
    private StateRecord RequireVersion(StateRecord record) => records.TryGetValue(record.Id, out var current) && current.Version == record.Version
        ? current : throw new OpenIddictExceptions.ConcurrencyException(Rejected);

    /// <summary>Native reference identifiers are already transformed by the manager and remain unique without a second hash.</summary>
    private void UniqueReference(StateRecord record)
    {
        if (record.ReferenceId is not null && records.Values.Any(current => current.Id != record.Id && current.ReferenceId == record.ReferenceId))
        {
            throw new InvalidOperationException(Rejected);
        }
    }

    /// <summary>Limits every retained field and native lifetime before admitting a state snapshot.</summary>
    private void Validate(StateRecord record)
    {
        if (record.Type != OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken ||
            record.Status is not (OpenIddictConstants.Statuses.Valid or OpenIddictConstants.Statuses.Redeemed or OpenIddictConstants.Statuses.Revoked) ||
            string.IsNullOrEmpty(record.Id) || record.Id.Length > BindingLimit || record.Payload?.Length > PayloadLimit ||
            new[] { record.ReferenceId, record.Subject, record.ApplicationId, record.AuthorizationId, record.Status, record.Type }.Any(value => value?.Length > ReferenceLimit) ||
            record.CreationDate is not { } created || record.ExpirationDate is not { } expires || created > clock.GetUtcNow() ||
            expires <= created || expires > created + Lifetime || record.Properties.Count > IdentifierLength ||
            record.Properties.Any(item => item.Key.Length > BindingLimit) || JsonSerializer.SerializeToUtf8Bytes(record.Properties).Length > PropertyLimit)
        {
            throw new InvalidOperationException(Rejected);
        }
    }

    /// <summary>Runs only with the ledger gate held; an expired record is no longer eligible native state.</summary>
    private long PruneCore(DateTimeOffset threshold, bool expiredOnly)
    {
        var now = clock.GetUtcNow();
        var expired = records.Values.Where(record => record.ExpirationDate <= now ||
            !expiredOnly && record.Status != OpenIddictConstants.Statuses.Valid && record.CreationDate < threshold).Select(record => record.Id).ToArray();
        foreach (var id in expired)
        {
            records.Remove(id);
        }
        return expired.LongLength;
    }
}
