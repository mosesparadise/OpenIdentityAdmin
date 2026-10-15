namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Reports only a fixed configuration category; underlying cryptographic/filesystem details remain private.</summary>
internal sealed class ProtectionMaterialException() : InvalidOperationException(SafeMessage)
{
    private const string SafeMessage = "Invalid protected key material.";
}
