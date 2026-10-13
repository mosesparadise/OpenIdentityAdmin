using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authorization;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Verifies the fixed current-factor proof before a restricted replacement transition.</summary>
/// <example><code>// Submit the native antiforgery-protected form at /account/administrator/security/replacement/verify.</code></example>
[Authorize(AuthenticationSchemes = AccountSchemes.Administrator)]
public sealed class ReplaceModel(IAdministratorSecurityJourney journey) : SecurityStepUpPage(journey, recovery: false);
