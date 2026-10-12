namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Reports unavailable factor operations without exposing material or protection diagnostics.</summary>
internal sealed class FactorUnavailableException : InvalidOperationException
{
    private const string SafeMessage = "Administrator factor operation is unavailable.";
    /// <summary>Creates a fixed diagnostic suitable for the existing safe account failure boundary.</summary>
    internal FactorUnavailableException() : base(SafeMessage) { }
}
