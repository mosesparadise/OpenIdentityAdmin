using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authorization;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Starts the fixed regeneration password journey without granting password-only authority.</summary>
/// <example><code>// GET /account/administrator/security/recovery-codes, then submit its native antiforgery-protected form.</code></example>
[Authorize(AuthenticationSchemes = AccountSchemes.Administrator)]
public sealed class RegenerationModel(IAdministratorSecurityJourney journey) : SecurityPasswordPage(journey, AdministratorSecurityAction.Regeneration);
