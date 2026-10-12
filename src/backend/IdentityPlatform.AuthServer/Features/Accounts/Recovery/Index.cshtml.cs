using IdentityPlatform.AuthServer.Features.Accounts.Shared;

namespace IdentityPlatform.AuthServer.Features.Accounts.Recovery;

/// <summary>Starts the fixed recovery password journey without granting password-only authority.</summary>
/// <example><code>// GET /account/administrator/recovery, then submit its native antiforgery-protected form.</code></example>
public sealed class IndexModel(IAdministratorSecurityJourney journey) : SecurityPasswordPage(journey, AdministratorSecurityAction.Recovery);
