using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Observes native context clearing on the existing stable Chromium session; it never resets or reconnects that session.</summary>
internal sealed class ApplicationDocumentLifetime
{
    private const string Enable = "Runtime.enable";
    private const string CreatedEvent = "Runtime.executionContextCreated";
    private const string DestroyedEvent = "Runtime.executionContextDestroyed";
    private const string ClearedEvent = "Runtime.executionContextsCleared";
    private const string Context = "context";
    private const string Auxiliary = "auxData";
    private const string Frame = "frameId";
    private const string Default = "isDefault";
    private const string Id = "id";
    private const string Unique = "uniqueId";
    private const string DestroyedId = "executionContextId";
    private const string DestroyedUnique = "executionContextUniqueId";
    private const int SetupSeconds = 3;
    private readonly ICDPSession session;
    internal ApplicationDocumentLedger Ledger
    {
        get;
    }

    /// <summary>Subscribes before one Runtime.enable command so the original default context is captured before submission.</summary>
    internal ApplicationDocumentLifetime(ICDPSession session, string frame)
    {
        this.session = session;
        Ledger = new(frame);
        session.Event(CreatedEvent).OnEvent += Created;
        session.Event(DestroyedEvent).OnEvent += Destroyed;
        session.Event(ClearedEvent).OnEvent += Cleared;
    }

    /// <summary>Enables observation once and arms the captured live original context; no disable/restore operation is available.</summary>
    internal async Task EnableAsync()
    {
        await session.SendAsync(Enable);
        await Ledger.Ready.WaitAsync(TimeSpan.FromSeconds(SetupSeconds));
        Ledger.Arm();
    }

    /// <summary>Reads only finite context correlation fields, discarding names, origins and other protocol payloads.</summary>
    private void Created(object? sender, JsonElement? message)
    {
        if (message is not { ValueKind: JsonValueKind.Object } value || !value.TryGetProperty(Context, out var context) ||
            context.ValueKind != JsonValueKind.Object || !context.TryGetProperty(Auxiliary, out var auxiliary) || auxiliary.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        Ledger.Created(Text(auxiliary, Frame), Number(context, Id), Text(context, Unique),
            auxiliary.TryGetProperty(Default, out var primary) && primary.ValueKind == JsonValueKind.True);
    }

    /// <summary>Requires both exact identifiers for individual destruction; unrelated realms cannot finish the original witness.</summary>
    private void Destroyed(object? sender, JsonElement? message)
    {
        if (message is { } value)
        {
            Ledger.Cleared(Number(value, DestroyedId), Text(value, DestroyedUnique));
        }
    }

    /// <summary>Records explicit clearing of the stable session's context epoch; returned original identity still invalidates acceptance.</summary>
    private void Cleared(object? sender, JsonElement? message) => Ledger.Cleared();

    /// <summary>Extracts only a bounded identifier candidate; the ledger owns validation.</summary>
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    /// <summary>Extracts an integer identity without converting arbitrary payload text.</summary>
    private static int Number(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out var id) ? id : 0;

    /// <summary>Freezes context observations before final ledger sealing and separate session detach.</summary>
    internal void Freeze()
    {
        session.Event(CreatedEvent).OnEvent -= Created;
        session.Event(DestroyedEvent).OnEvent -= Destroyed;
        session.Event(ClearedEvent).OnEvent -= Cleared;
    }
}
