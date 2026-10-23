using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Requires the exact dormant browser manifest without widening existing initialization or live B2 admission.</summary>
public sealed class AdministratorBrowserRegistrationTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Callback = "https://administrator.example.test/callback";
    private const string ReturnUri = "https://administrator.example.test/bff/logout/return";
    private const string BrowserProfile = "administrator-browser-v1";
    private const string LegacyProfile = "administrator-v1";
    private const string LegacyPermission = "ept:logout";
    private const string ProfileProperty = "profile";
    private const string EnabledProperty = "enabled";
    private const string PrivateExponent = "AQAB";
    private const string ChangedCaseReturn = "https://administrator.example.test/bff/Logout/return";
    private const string EscapedCallback = "https://administrator.example.test/bff/%6Cogout/return";
    private const string ExtraValue = "unexpected-browser-metadata";

    /// <summary>Only the explicit browser variant adds logout metadata, and each variant rejects the other's descriptor.</summary>
    [Fact]
    public void Given_B2Policy_When_BrowserVariantIsSelected_Then_OnlyTheExactNewManifestMatches()
    {
        var legacy = AdministratorRegistrationPolicy.Create(Callback, fixture.Key)!;
        var browser = legacy.ForBrowserSessions();
        Assert.NotNull(browser);
        var original = legacy.Application();
        var application = browser.Application();
        var scope = AdministratorRegistrationPolicy.Scope();
        Assert.True(legacy.Matches(original, scope));
        Assert.False(legacy.Matches(application, scope));
        Assert.False(browser.Matches(original, scope));
        Assert.True(browser.Matches(application, scope));
        Assert.Empty(original.PostLogoutRedirectUris);
        Assert.Equal(ReturnUri, Assert.Single(application.PostLogoutRedirectUris).OriginalString);
        Assert.Equal([OpenIddictConstants.Permissions.Endpoints.EndSession], application.Permissions.Except(original.Permissions));
        Assert.Empty(original.Permissions.Except(application.Permissions));
        Assert.Equal(LegacyProfile, original.Properties[ProfileProperty].GetString());
        Assert.Equal(BrowserProfile, application.Properties[ProfileProperty].GetString());
        Assert.Equal(LegacyProfile, scope.Properties[ProfileProperty].GetString());
        Assert.Equal(original.Settings, application.Settings);
        Assert.True(original.Requirements.SetEquals(application.Requirements));
        Assert.Equal(original.RedirectUris, application.RedirectUris);
        Assert.Equal(legacy.Key.Fingerprint, browser.Key.Fingerprint);
        Assert.True(browser.ForBrowserSessions()!.Matches(application, scope));
    }

    /// <summary>Independent expected destinations retain the HTTPS authority and port but exclude callback path and query.</summary>
    [Theory]
    [InlineData("https://administrator.example.test/nested/callback?fixed=value", "https://administrator.example.test/bff/logout/return")]
    [InlineData("https://administrator.example.test:7443/nested/callback", "https://administrator.example.test:7443/bff/logout/return")]
    [InlineData("https://[::1]:7443/callback?fixed=value", "https://[::1]:7443/bff/logout/return")]
    public void Given_ValidatedCallback_When_ReturnUriIsDerived_Then_OnlyItsAuthorityIsRetained(string callback, string expected)
    {
        var policy = AdministratorRegistrationPolicy.Create(callback, fixture.Key)!.ForBrowserSessions();
        Assert.NotNull(policy);
        Assert.Equal(expected, Assert.Single(policy.Application().PostLogoutRedirectUris).OriginalString);
        Assert.Equal(callback, Assert.Single(policy.Application().RedirectUris).OriginalString);
    }

    /// <summary>The browser variant cannot assign the native callback and logout return to the same routed path.</summary>
    [Theory]
    [InlineData("https://administrator.example.test/bff/logout/return")]
    [InlineData("https://administrator.example.test/bff/logout/return?fixed=value")]
    [InlineData("https://administrator.example.test/BFF/LOGOUT/RETURN/")]
    public void Given_ReservedReturnCallback_When_BrowserVariantIsSelected_Then_TheCollisionIsDenied(string callback)
    {
        var legacy = AdministratorRegistrationPolicy.Create(callback, fixture.Key);
        Assert.NotNull(legacy);
        Assert.Null(legacy.ForBrowserSessions());
        Assert.True(legacy.Matches(legacy.Application(), AdministratorRegistrationPolicy.Scope()));
    }

    /// <summary>The original canonical URI gate already denies escaped unreserved path substitutions before any variant can be selected.</summary>
    [Fact]
    public void Given_NoncanonicalEscapedCallback_When_Configured_Then_TheOriginalPolicyAlreadyDeniesIt() =>
        Assert.Null(AdministratorRegistrationPolicy.Create(EscapedCallback, fixture.Key));

    /// <summary>Fresh descriptors and sibling variants cannot be changed through a previously returned native object.</summary>
    [Fact]
    public void Given_BrowserDescriptor_When_CallerMutatesIt_Then_PoliciesAndLaterDescriptorsRemainIndependent()
    {
        var legacy = AdministratorRegistrationPolicy.Create(Callback, fixture.Key)!;
        var browser = legacy.ForBrowserSessions()!;
        var sibling = legacy.ForBrowserSessions()!;
        var application = browser.Application();
        application.PostLogoutRedirectUris.Clear();
        application.Properties[ProfileProperty] = JsonSerializer.SerializeToElement(ExtraValue);
        application.JsonWebKeySet!.Keys[0].Kid = ExtraValue;
        var scope = AdministratorRegistrationPolicy.Scope();
        Assert.False(browser.Matches(application, scope));
        Assert.True(browser.Matches(browser.Application(), scope));
        Assert.True(sibling.Matches(sibling.Application(), scope));
        Assert.True(legacy.Matches(legacy.Application(), scope));
    }

    /// <summary>Independent hostile metadata must fail even when all other B3 fields and the original key remain valid.</summary>
    [Theory]
    [InlineData(BrowserRegistrationDrift.MissingReturn)]
    [InlineData(BrowserRegistrationDrift.ExtraReturn)]
    [InlineData(BrowserRegistrationDrift.ReturnCase)]
    [InlineData(BrowserRegistrationDrift.MissingPermission)]
    [InlineData(BrowserRegistrationDrift.LegacyPermission)]
    [InlineData(BrowserRegistrationDrift.ApplicationMarker)]
    [InlineData(BrowserRegistrationDrift.ScopeMarker)]
    [InlineData(BrowserRegistrationDrift.DisabledApplication)]
    [InlineData(BrowserRegistrationDrift.DisabledScope)]
    [InlineData(BrowserRegistrationDrift.ExtraProperty)]
    [InlineData(BrowserRegistrationDrift.RefreshPermission)]
    [InlineData(BrowserRegistrationDrift.ExtraSetting)]
    [InlineData(BrowserRegistrationDrift.MissingPar)]
    [InlineData(BrowserRegistrationDrift.PrivateKey)]
    public void Given_BrowserManifest_When_SecurityMetadataDrifts_Then_ExactMatchingDenies(BrowserRegistrationDrift drift)
    {
        var policy = AdministratorRegistrationPolicy.Create(Callback, fixture.Key)!.ForBrowserSessions()!;
        var application = policy.Application();
        var scope = AdministratorRegistrationPolicy.Scope();
        Assert.True(policy.Matches(application, scope));
        Change(application, scope, drift);
        Assert.False(policy.Matches(application, scope));
        Assert.True(policy.Matches(policy.Application(), AdministratorRegistrationPolicy.Scope()));
    }

    /// <summary>Changes only a destination, endpoint permission or marker while retaining unrelated valid metadata.</summary>
    private static void Change(OpenIddictApplicationDescriptor application, OpenIddictScopeDescriptor scope, BrowserRegistrationDrift drift)
    {
        switch (drift)
        {
            case BrowserRegistrationDrift.MissingReturn:
                application.PostLogoutRedirectUris.Clear();
                break;
            case BrowserRegistrationDrift.ExtraReturn:
                application.PostLogoutRedirectUris.Add(new Uri(Callback));
                break;
            case BrowserRegistrationDrift.ReturnCase:
                application.PostLogoutRedirectUris.Clear();
                application.PostLogoutRedirectUris.Add(new Uri(ChangedCaseReturn));
                break;
            case BrowserRegistrationDrift.MissingPermission:
                application.Permissions.Remove(OpenIddictConstants.Permissions.Endpoints.EndSession);
                break;
            case BrowserRegistrationDrift.LegacyPermission:
                application.Permissions.Remove(OpenIddictConstants.Permissions.Endpoints.EndSession);
                application.Permissions.Add(LegacyPermission);
                break;
            case BrowserRegistrationDrift.ApplicationMarker:
                application.Properties[ProfileProperty] = JsonSerializer.SerializeToElement(LegacyProfile);
                break;
            case BrowserRegistrationDrift.ScopeMarker:
                scope.Properties[ProfileProperty] = JsonSerializer.SerializeToElement(BrowserProfile);
                break;
            default:
                ChangeAuthority(application, scope, drift);
                break;
        }
    }

    /// <summary>Seeds disabled state, grants, lifetime settings, PAR requirements or private-key contamination independently.</summary>
    private static void ChangeAuthority(OpenIddictApplicationDescriptor application, OpenIddictScopeDescriptor scope, BrowserRegistrationDrift drift)
    {
        switch (drift)
        {
            case BrowserRegistrationDrift.DisabledApplication:
                application.Properties[EnabledProperty] = JsonSerializer.SerializeToElement(false);
                break;
            case BrowserRegistrationDrift.DisabledScope:
                scope.Properties[EnabledProperty] = JsonSerializer.SerializeToElement(false);
                break;
            case BrowserRegistrationDrift.ExtraProperty:
                application.Properties[ExtraValue] = JsonSerializer.SerializeToElement(true);
                break;
            case BrowserRegistrationDrift.RefreshPermission:
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                break;
            case BrowserRegistrationDrift.ExtraSetting:
                application.Settings[ExtraValue] = ExtraValue;
                break;
            case BrowserRegistrationDrift.MissingPar:
                application.Requirements.Remove(OpenIddictConstants.Requirements.Features.PushedAuthorizationRequests);
                break;
            case BrowserRegistrationDrift.PrivateKey:
                application.JsonWebKeySet!.Keys[0].D = PrivateExponent;
                break;
        }
    }
}

/// <summary>Names independent browser registration drift boundaries.</summary>
public enum BrowserRegistrationDrift
{
    MissingReturn, ExtraReturn, ReturnCase, MissingPermission, LegacyPermission, ApplicationMarker, ScopeMarker,
    DisabledApplication, DisabledScope, ExtraProperty, RefreshPermission, ExtraSetting, MissingPar, PrivateKey
}
