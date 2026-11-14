using System.Collections.Immutable;
using System.Text.Json;

namespace IdentityPlatform.Sample.Client.Features.State;

/// <summary>Detached native state-token data; the ledger owns versions and never shares retained instances.</summary>
internal sealed class StateRecord
{
    private const string CompactGuidFormat = "N";

    internal string Id { get; set; } = Guid.NewGuid().ToString(CompactGuidFormat);
    internal long Version
    {
        get; set;
    }
    internal string? ApplicationId
    {
        get; set;
    }
    internal string? AuthorizationId
    {
        get; set;
    }
    internal string? ReferenceId
    {
        get; set;
    }
    internal string? Subject
    {
        get; set;
    }
    internal string? Status
    {
        get; set;
    }
    internal string? Type
    {
        get; set;
    }
    internal string? Payload
    {
        get; set;
    }
    internal DateTimeOffset? CreationDate
    {
        get; set;
    }
    internal DateTimeOffset? ExpirationDate
    {
        get; set;
    }
    internal DateTimeOffset? RedemptionDate
    {
        get; set;
    }
    internal ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;

    /// <summary>Copies values and JSON ownership so native callers cannot mutate a retained record without CAS.</summary>
    internal StateRecord Copy() => new()
    {
        Id = Id,
        Version = Version,
        ApplicationId = ApplicationId,
        AuthorizationId = AuthorizationId,
        ReferenceId = ReferenceId,
        Subject = Subject,
        Status = Status,
        Type = Type,
        Payload = Payload,
        CreationDate = CreationDate,
        ExpirationDate = ExpirationDate,
        RedemptionDate = RedemptionDate,
        Properties = Properties.ToImmutableDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal)
    };
}
