using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authorization;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Starts the fixed replacement password journey without granting password-only authority.</summary>
/// <example><code>// GET /account/administrator/security/replacement, then submit its native antiforgery-protected form.</code></example>
[Authorize(AuthenticationSchemes = AccountSchemes.Administrator)]
public sealed class ReplacementModel(IAdministratorSecurityJourney journey) : SecurityPasswordPage(journey, AdministratorSecurityAction.Replacement);
