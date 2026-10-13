namespace IdentityPlatform.AuthServer.Features.Accounts.Recovery;

/// <summary>Verifies the fixed recovery-code proof before a restricted replacement transition.</summary>
/// <example><code>// Submit the native antiforgery-protected form at /account/administrator/recovery/verify.</code></example>
public sealed class VerifyModel(IAdministratorSecurityJourney journey) : SecurityStepUpPage(journey, recovery: true);
