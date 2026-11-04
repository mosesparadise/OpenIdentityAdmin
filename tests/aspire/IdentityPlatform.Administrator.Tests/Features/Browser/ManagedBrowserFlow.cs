using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Owns one test relying-party ceremony without serializing private proof or credential values in diagnostics.</summary>
internal sealed class ManagedBrowserFlow(ApplicationEditorRegistration registration, string subject)
{
    private const string MissingExchange = "The managed browser exchange did not complete.";
    private const string StateField = "state";
    private const string NonceField = "nonce";
    private const string AccessField = "access_token";
    private const string IdentityField = "id_token";
    private const int LifetimeSeconds = 300;
    private int started;
    private int callbacks;
    private int exchanges;
    private JsonElement? body;
    internal ApplicationEditorRegistration Registration => registration;
    internal string Subject => subject;
    internal string Verifier { get; } = ManagedAuthorizationRequest.Random();
    internal string BrowserSecret { get; } = ManagedAuthorizationRequest.Random();
    internal Dictionary<string, string> Initial { get; private set; } = [];
    internal string State => Initial[StateField];
    internal string Nonce => Initial[NonceField];
    internal DateTimeOffset ExpiresAt { get; } = DateTimeOffset.UtcNow.AddSeconds(LifetimeSeconds);
    internal TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Callbacks => Volatile.Read(ref callbacks);
    internal int Exchanges => Volatile.Read(ref exchanges);
    internal bool Started => Volatile.Read(ref started) == 1;
    internal JsonElement Body => body ?? throw new InvalidOperationException(MissingExchange);
    internal string[] Secrets => body is { } value ? [.. registration.Secrets, value.GetProperty(AccessField).GetString()!, value.GetProperty(IdentityField).GetString()!] : registration.Secrets;

    /// <summary>Fixes the exact current UI callback and independent PKCE/state/nonce before opening the browser entry.</summary>
    internal void Initialize() => Initial = ManagedAuthorizationRequest.Initial(Verifier, registration.ClientId, registration.Callback);

    /// <summary>Allows one start within the original relying-party correlation deadline.</summary>
    internal bool TryStart() => ExpiresAt > DateTimeOffset.UtcNow && Interlocked.Exchange(ref started, 1) == 0;

    /// <summary>Counts a callback independently of success and refuses duplicate code exchange.</summary>
    internal bool TryCallback() => Interlocked.Increment(ref callbacks) == 1;

    /// <summary>Records the sole server-side exchange before issuing its real HTTP request.</summary>
    internal void Exchange() => Interlocked.Increment(ref exchanges);

    /// <summary>Retains validated native tokens only in server-side test memory and signals joined completion.</summary>
    internal void Complete(JsonElement value)
    {
        body = value.Clone();
        Completion.TrySetResult(true);
    }
}
