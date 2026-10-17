namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Flows an explicit owned capability to native service-created scopes without changing the application container.</summary>
internal sealed record ProtocolStorageAmbient(ProtocolStorageRequest Request, ProtocolStorageScope Scope)
{
    private static readonly AsyncLocal<ProtocolStorageAmbient?> CurrentValue = new();
    internal static ProtocolStorageAmbient? Current
    {
        /// <summary>Reads only the current execution-context capability.</summary>
        get => CurrentValue.Value;
        /// <summary>Restores or replaces the capability in the current execution context.</summary>
        set => CurrentValue.Value = value;
    }
}
