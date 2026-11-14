using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Requires native bearer denial after each independent current-authority invalidation without returning stale identity data.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedUserInfoDenialTests(BootstrapFixture fixture)
{
    private const string CurrentPolicyBypass = "managed-userinfo-current-policy-bypass";
    private const string Requested = "email openid";
    private const string AccessField = "access_token";
    private const string AccessType = "urn:ietf:params:oauth:token-type:access_token";
    private const string Revoked = "revoked";
    private const string IdentityKind = "identity";
    private const string ChangedConsent = "Changed consent";

    /// <summary>Actual signed unexpired credentials do not override current revoked native entries or changed managed/account policy.</summary>
    [Theory]
    [InlineData("PostgreSql", UserInfoInvalidation.Token, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoInvalidation.Grant, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoInvalidation.Application, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoInvalidation.Account, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoInvalidation.Scope, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoInvalidation.ConsentText, Traits = ["Provider", "PostgreSql", "CriticalDecision", "managed-userinfo-current-policy"])]
    [InlineData("SqlServer", UserInfoInvalidation.Token, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoInvalidation.Grant, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoInvalidation.Application, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoInvalidation.Account, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoInvalidation.Scope, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoInvalidation.ConsentText, Traits = ["Provider", "SqlServer", "CriticalDecision", "managed-userinfo-current-policy"])]
    public async Task Given_ConsentedNativeAccessToken_When_CurrentAuthorityIsInvalidated_Then_UserInfoReturnsNativeBearerDenial(string provider, UserInfoInvalidation change)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await ManagedConsentTest.SeedAsync(test);
        await ManagedConsentTest.EmailAsync(test, ManagedConsentTest.Address, true);
        var flow = new ManagedTokenFlow(test, Requested);
        await flow.ApproveAsync();
        using var exchange = await flow.ExchangeAsync(await flow.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var access = body.RootElement.GetProperty(AccessField).GetString()!;
        var subject = Assert.Single((await test.SnapshotAsync()).Interactions).UserId!;
        using var control = await ManagedConsentTest.ReadAsync(test, access);
        await ManagedConsentTest.RequireAsync(control, subject, ManagedConsentTest.Address, true);
        await InvalidateAsync(test, change, subject);
        var before = await ManagedUserInfoEvidence.CaptureAsync(test);
        using var denied = await ManagedConsentTest.ReadAsync(test, access);
        if (change == UserInfoInvalidation.ConsentText && denied.StatusCode == HttpStatusCode.OK)
        {
            await ManagedConsentTest.RequireAsync(denied, subject, ManagedConsentTest.Address, true);
            await before.RequireAsync(test, commits: 1);
            Assert.Fail(CurrentPolicyBypass);
        }
        await ManagedConsentTest.DeniedAsync(denied);
        await before.RequireAsync(test, commits: 0);
    }

    /// <summary>Changes one independently persisted authority field without advancing revisions, exercising mandatory fresh current-state reads.</summary>
    private static async Task InvalidateAsync(ManagedCacheTestContext test, UserInfoInvalidation change, string subject)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        switch (change)
        {
            case UserInfoInvalidation.Token:
                (await database.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync(row => row.Application!.Id == test.Id &&
                    row.Type == AccessType, token)).Status = Revoked;
                break;
            case UserInfoInvalidation.Grant:
                (await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().SingleAsync(row => row.Application!.Id == test.Id, token)).Status = Revoked;
                break;
            case UserInfoInvalidation.Application:
                (await database.Set<ApplicationPolicy>().SingleAsync(row => row.ApplicationId == test.Id, token)).Enabled = false;
                break;
            case UserInfoInvalidation.Account:
                (await database.Users.SingleAsync(row => row.Id == subject, token)).IsEnabled = false;
                break;
            case UserInfoInvalidation.Scope:
                (await database.Set<ScopePolicy>().SingleAsync(row => row.Kind == IdentityKind, token)).Enabled = false;
                break;
            case UserInfoInvalidation.ConsentText:
                (await database.Set<ScopePolicy>().SingleAsync(row => row.Kind == IdentityKind, token)).ConsentText = ChangedConsent;
                break;
        }
        await database.SaveChangesAsync(token);
    }
}

/// <summary>Names independent ordinary invalidation paths without encoding infrastructure faults as bearer denials.</summary>
public enum UserInfoInvalidation
{
    Token, Grant, Application, Account, Scope, ConsentText
}
