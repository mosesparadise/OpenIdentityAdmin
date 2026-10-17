namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Bounds the finite nonstreaming protocol response retained before admission.</summary>
internal static class ProtocolResponseLimits
{
    internal const int BodyBytes = 64 * 1024;
    internal const int HeaderBytes = 16 * 1024;
    internal const int Headers = 32;
    internal const int Callbacks = 16;
}
