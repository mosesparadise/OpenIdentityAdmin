using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Uses actual native transport and independently verifies browser-only claims against the persisted completed session.</summary>
internal static class BrowserProtocolFlow
{
    internal const string Expiry = "admin_session_expires_at";
    internal const string Intent = "admin_login_intent";
    private const string Session = "sid";
    internal const string Origin = "oi_b3_request_id";

    /// <summary>Submits a real PAR reference with the fresh cookie by default; the prior same-user cookie is a separate negative.</summary>
    internal static Task<NativeAccountResult> AuthorizeAsync(NativeBrowserParFixture browser, string reference, bool fresh = true) =>
        NativeAccountHttp.InvokeAsync(browser.Scenario, AdministratorProtocolScenario.AuthorizePath, new()
        {
            [OpenIddictConstants.Parameters.ClientId] = AdministratorProtocolIdentity.ClientId,
            [OpenIddictConstants.Parameters.RequestUri] = reference
        }, services: browser.Services, configureHttp: http => http.Request.Headers.Cookie = fresh ? browser.FreshCookie : browser.Scenario.Cookie);

    /// <summary>Creates and independently decrypts the native code before exchange, proving origin binding is distinct from code identity.</summary>
    internal static async Task<string> CodeAsync(NativeBrowserParFixture browser)
    {
        var row = await browser.RowAsync();
        var reference = await browser.RequireAttachedAsync(await browser.PushAsync(), row, browser.Journey.Clock.Now.ToUnixTimeSeconds());
        var code = NativeAccountFlow.ReadCode(browser.Scenario, await AuthorizeAsync(browser, reference));
        var principal = await NativeAccountFlow.ValidateAsync(browser.Scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, browser.Services);
        Assert.True(browser.Binding.Matches(principal));
        var attached = await browser.RowAsync();
        Assert.True(principal.GetClaim(Origin) == attached.RequestId && principal.GetTokenId() != attached.RequestId);
        Assert.DoesNotContain(principal.Claims, claim => claim.Type is Expiry or Intent);
        Assert.True(principal.GetExpirationDate() <= DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt));
        return code;
    }

    /// <summary>Validates actual returned ID/access artifacts and exact numeric source expiry; raw selectors never leave private protocol artifacts.</summary>
    internal static async Task RequireTokensAsync(NativeBrowserParFixture browser, NativeAccountResult response, bool bound = true)
    {
        NativeAccountFlow.RequireSuccess(response, StatusCodes.Status200OK);
        Assert.False(response.HasCookie);
        using var json = JsonDocument.Parse(response.Body);
        var accessValue = json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!;
        var access = await NativeAccountFlow.ValidateAsync(browser.Scenario, accessValue, OpenIddictConstants.TokenTypeIdentifiers.AccessToken, browser.Services);
        var identity = await NativeIdentityTokenOracle.ValidateAsync(browser.Scenario,
            json.RootElement.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()!, accessValue);
        foreach (var principal in new[] { access, identity })
        {
            Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read(principal, out _));
            Assert.DoesNotContain(principal.Claims, claim => claim.Type == Origin || claim.Value == browser.Binding.Intent || claim.Value == browser.Binding.Completion);
        }
        Assert.DoesNotContain(access.Claims, claim => claim.Type is Intent or Expiry);
        if (bound)
        {
            RequireIdentity(identity, await browser.RowAsync());
        }
        else
        {
            Assert.DoesNotContain(identity.Claims, claim => claim.Type is Intent or Expiry);
        }
    }

    /// <summary>Separates original source expiry from the shorter token deadline and validates against durable fresh-session provenance.</summary>
    internal static void RequireIdentity(ClaimsPrincipal identity, IssuerContinuation row)
    {
        Assert.True(identity.GetClaim(OpenIddictConstants.Claims.Subject) == row.UserId && identity.GetClaim(Session) == row.SessionId);
        Assert.True(identity.GetClaim(Intent) == row.IntentDigest);
        var expiry = Assert.Single(identity.FindAll(Expiry));
        Assert.True(expiry.ValueType is ClaimValueTypes.Integer32 or ClaimValueTypes.Integer64);
        Assert.True(expiry.Value == row.SessionExpiresAt!.Value.ToString(CultureInfo.InvariantCulture));
        var tokenExpiry = long.Parse(identity.GetClaim(OpenIddictConstants.Claims.ExpiresAt)!, CultureInfo.InvariantCulture);
        Assert.True(tokenExpiry <= row.ExpiresAt && tokenExpiry < row.SessionExpiresAt);
    }
}
