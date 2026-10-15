using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.AspNetCore.Builder;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Complements native F13 HTTP controls with the entire mapped AuthServer endpoint inventory.</summary>
public sealed class ProductionRoutesTests
{
    private static readonly string[] Health = ["/alive", "/health"];
    private static readonly string[] Assets = ["/assets/tokens.css", "/assets/theme.js", "/assets/login.css", "/assets/login.js"];
    private static readonly string[] Accounts =
    [
        "/account/administrator/login", "/account/administrator/enrollment", "/account/administrator/mfa",
        "/account/administrator/security", "/account/administrator/recovery", "/account/administrator/recovery/verify",
        "/account/administrator/logout", "/account/administrator/security/replacement",
        "/account/administrator/security/replacement/verify", "/account/administrator/security/replacement/confirm",
        "/account/administrator/security/recovery-codes", "/account/administrator/security/recovery-codes/verify"
    ];

    /// <summary>Starts unmodified real production composition, then rejects any management, documentation, pilot or driver route outside its finite inventory.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AdministratorFeatureSetting_When_ProductionAuthServerStarts_Then_ExactAllowedRoutesAreMapped(bool administrator)
    {
        WebApplication? composed = null;
        await using var host = await AccountHost.StartAsync(pilot: false, administrator: administrator,
            configure: app => composed = app);
        Assert.NotNull(composed);
        ProductionEndpointInventory.RequireOnly(composed, administrator ? Health.Concat(Assets).Concat(Accounts) : Health);
        ProductionEndpointInventory.RequireNoDriverReference(typeof(IssuerCredentials).Assembly);
    }
}
