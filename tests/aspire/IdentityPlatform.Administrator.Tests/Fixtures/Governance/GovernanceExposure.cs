using System.Net;
using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Combines actual child-process HTTP observations with separate explicit compiled-boundary checks.</summary>
/// <remarks>Mandatory Host/Contracts ProductionRoutesTests complete exhaustive metadata inspection separately.</remarks>
internal static class GovernanceExposure
{
    private const string AdministratorSetting = "Identity__AdministratorAccounts__Enabled";
    private const string EnabledValue = "true";
    private const string PilotFailure = "The genuine pilot positive control did not validate.";
    private const string RedirectFailure = "A redirect cannot establish an unmapped management route.";
    private const string Auth = "auth";
    private const string Api = "admin-api";
    private const string Bff = "admin-bff";
    private const string Https = "https";
    private const string Security = "/account/administrator/security";
    private const string SetCookie = "Set-Cookie";
    private const string DriverAssembly = "IdentityPlatform.Governance.Driver";
    private const int HealthSeconds = 30;
    private static readonly string[] Closed =
    [
        "/api/v1/administrators", "/api/v1/users", "/api/v1/roles", "/bff/governance",
        "/governance", "/governance/execute", "/internal/governance", "/debug/governance", "/force/governance",
        "/docs/scalar", "/docs/swagger", "/openapi/v1.json"
    ];
    private static readonly HttpMethod[] Methods = [HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete];

    /// <summary>Uses the existing real full Aspire graph and genuine pilot login; administrator access is enabled only inside this disposable test graph.</summary>
    internal static async Task RequireRealHostsAsync(string provider)
    {
        var token = TestContext.Current.CancellationToken;
        await using var environment = await IssuerEnvironment.CreateAsync(provider, token, configureBeforeBuild: (builder, _) =>
        {
            builder.CreateResourceBuilder<ProjectResource>(Auth).WithEnvironment(AdministratorSetting, EnabledValue);
            return Task.CompletedTask;
        });
        using var pilot = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(pilot, exchange: true);
        Assert.True(flow.Validated, PilotFailure);
        using var denied = await pilot.GetAsync(new Uri(environment.Issuer, Security), token);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
        Assert.False(denied.Headers.Contains(SetCookie));
        await RequireClosedAsync(pilot, environment.Issuer);
        using var health = CancellationTokenSource.CreateLinkedTokenSource(token);
        health.CancelAfter(TimeSpan.FromSeconds(HealthSeconds));
        foreach (var role in new[] { Api, Bff })
        {
            await environment.Application.ResourceNotifications.WaitForResourceHealthyAsync(role, health.Token);
            using var client = environment.Application.CreateHttpClient(role, Https);
            await RequireClosedAsync(client, environment.Application.GetEndpoint(role, Https));
        }
    }

    /// <summary>Requires direct absent-route responses, disallowing redirects or cookie issuance as substitute evidence.</summary>
    private static async Task RequireClosedAsync(HttpClient client, Uri origin)
    {
        foreach (var path in Closed)
        {
            foreach (var method in Methods)
            {
                var target = new Uri(origin, path);
                using var request = new HttpRequestMessage(method, target);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.True(response.RequestMessage?.RequestUri == target, RedirectFailure);
                Assert.Null(response.Headers.Location);
                Assert.False(response.Headers.Contains(SetCookie));
            }
        }
    }

    /// <summary>Rejects new public proof constructors and direct production references to the test-only executable.</summary>
    internal static void RequirePrivateCapabilities()
    {
        Type[] capabilities = [typeof(GovernanceProof), typeof(GovernanceVerification), typeof(GovernanceCommit), typeof(GovernanceDenial)];
        foreach (var capability in capabilities)
        {
            Assert.False(capability.IsVisible);
            var constructors = capability.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotEmpty(constructors);
            Assert.All(constructors, constructor => Assert.True(constructor.IsPrivate));
        }
        Assert.DoesNotContain(typeof(GovernanceService).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == DriverAssembly);
    }
}
