namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Names the explicit publication obligation selected by the native protocol integration.</summary>
internal enum ProtocolResponseOutcome
{
    Unspecified,
    ReadOnly,
    Committed,
    NativeError
}
