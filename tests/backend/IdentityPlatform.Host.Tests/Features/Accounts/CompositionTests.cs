using System.Net;
using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Checks the actual composed routes/default schemes and strict non-sensitive feature configuration.</summary>
public sealed class CompositionTests
{
    private const string ExpectedPilotScheme = "Identity.Application";
    private const string ExpectedAnonymousScheme = "IdentityPlatform.Accounts.None";
    private const string Alive = "/alive";
    private const string Login = "/account/login";
    private const string AdministratorLogin = "/account/administrator/login";
    private static readonly string[] AdministratorSecurityRoutes = ["/account/administrator/recovery", "/account/administrator/recovery/verify",
        "/account/administrator/security/replacement", "/account/administrator/security/replacement/verify",
        "/account/administrator/security/replacement/confirm", "/account/administrator/security/recovery-codes",
        "/account/administrator/security/recovery-codes/verify", "/account/administrator/logout"];
    private static readonly string[] UnavailableRoutes = ["/docs/scalar", "/docs/swagger", "/api/v1/users",
        "/account/administrator/browser/login", "/account/administrator/browser/mfa"];
    private static readonly string[] ProtocolRoutes = ["/.well-known/openid-configuration", "/connect/authorize", "/connect/token"];

    /// <summary>Exercises all switch combinations against compiled Razor routing and native authentication defaults.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_FeatureSwitches_When_HostStarts_Then_OnlyEnabledFamiliesAreComposed(bool pilot, bool administrator)
    {
        await using var host = await AccountHost.StartAsync(pilot, administrator);
        using var alive = await host.Client.GetAsync(Alive, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        using var login = await host.Client.GetAsync(Login, TestContext.Current.CancellationToken);
        Assert.Equal(pilot ? HttpStatusCode.BadRequest : HttpStatusCode.NotFound, login.StatusCode);
        foreach (var route in UnavailableRoutes.Concat(pilot ? [] : ProtocolRoutes))
        {
            using var response = await host.Client.GetAsync(route, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var administratorLogin = await host.Client.GetAsync(AdministratorLogin, TestContext.Current.CancellationToken);
        Assert.Equal(administrator ? HttpStatusCode.OK : HttpStatusCode.NotFound, administratorLogin.StatusCode);
        if (administrator)
        {
            var html = await administratorLogin.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("<span class=\"badge\">Account access</span>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<span class=\"badge\">Development</span>", html, StringComparison.Ordinal);
        }
        if (!administrator)
        {
            foreach (var route in AdministratorSecurityRoutes)
            {
                using var response = await host.Client.GetAsync(route, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }
        await RequireSchemesAsync(host.Services, pilot, administrator);
    }

    /// <summary>Checks scheme-provider resolution, including the framework's automatic single-scheme default behavior.</summary>
    private static async Task RequireSchemesAsync(IServiceProvider services, bool pilot, bool administrator)
    {
        var schemes = services.GetService<IAuthenticationSchemeProvider>();
        if (!pilot && !administrator)
        {
            Assert.Null(schemes);
            return;
        }
        Assert.NotNull(schemes);
        Assert.Equal(pilot ? ExpectedPilotScheme : ExpectedAnonymousScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())?.Name);
        Assert.Equal(pilot ? ExpectedPilotScheme : ExpectedAnonymousScheme, (await schemes.GetDefaultChallengeSchemeAsync())?.Name);
        Assert.Equal(administrator, await schemes.GetSchemeAsync(AccountSchemes.Administrator) is not null);
    }

    /// <summary>Rejects malformed booleans without echoing attacker-controlled configuration values.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData(" true ")]
    [InlineData("synthetic-private-setting")]
    [InlineData("true")]
    public void Given_InvalidOrUnprotectedActivation_When_Configured_Then_SafeFailureIsReturned(string value)
    {
        using var configuration = new ConfigurationManager();
        configuration[AccountHost.AdministratorSetting] = value;
        var failure = Assert.Throws<AdministratorConfigurationException>(() => AdministratorConfiguration.IsEnabled(configuration, null));
        Assert.Equal("Invalid administrator account configuration. Explicit activation requires configured issuer protection.", failure.Message);
    }

    /// <summary>Disables absent/false settings and rejects ambiguous section values and unknown nested fields.</summary>
    [Fact]
    public void Given_ConfigurationShapes_When_Validated_Then_UnknownFieldsFailClosed()
    {
        using var configuration = new ConfigurationManager();
        Assert.False(AdministratorConfiguration.IsEnabled(configuration, null));
        configuration[AccountHost.AdministratorSetting] = bool.FalseString;
        Assert.False(AdministratorConfiguration.IsEnabled(configuration, null));
        const string Unknown = "Identity:AdministratorAccounts:Unknown";
        configuration[Unknown] = bool.TrueString;
        Assert.Throws<AdministratorConfigurationException>(() => AdministratorConfiguration.IsEnabled(configuration, null));
        configuration[Unknown] = null;
        configuration[AdministratorConfiguration.Section] = bool.TrueString;
        Assert.Throws<AdministratorConfigurationException>(() => AdministratorConfiguration.IsEnabled(configuration, null));
    }

    /// <summary>Proves disabled/unknown pages and aliases have no selectors; approved future pages require their own switch.</summary>
    [Theory]
    [InlineData(false, false, 0, 0)]
    [InlineData(false, true, 0, 1)]
    [InlineData(true, false, 1, 0)]
    [InlineData(true, true, 1, 1)]
    public void Given_CompiledPageModels_When_ConventionsRun_Then_OnlyExplicitRoutesSurvive(bool pilot, bool administrator, int pilotCount, int adminCount)
    {
        const string PilotPage = "/Login/Index";
        const string AdminPage = "/Enrollment/Index";
        const string AdminRoute = "account/administrator/enrollment";
        const string PilotRoute = "account/login";
        const string UnknownPage = "/Unexpected/Index";
        const string Alias = "unexpected-alias";
        var convention = new AccountPageConvention(pilot, administrator);
        var login = Page(PilotPage, PilotRoute, Alias);
        var enrollment = Page(AdminPage, AdminRoute, Alias);
        var unknown = Page(UnknownPage, AdminRoute);
        convention.Apply(login);
        convention.Apply(enrollment);
        convention.Apply(unknown);
        Assert.Equal(pilotCount, login.Selectors.Count);
        Assert.Equal(adminCount, enrollment.Selectors.Count);
        Assert.Empty(unknown.Selectors);
    }

    /// <summary>Creates native route models without adding production placeholder pages.</summary>
    private static PageRouteModel Page(string page, params string[] routes)
    {
        const string Root = "/Features/Accounts";
        const string Extension = ".cshtml";
        var model = new PageRouteModel(Root + page + Extension, page);
        foreach (var route in routes)
        {
            model.Selectors.Add(new SelectorModel { AttributeRouteModel = new AttributeRouteModel { Template = route } });
        }
        return model;
    }
}
