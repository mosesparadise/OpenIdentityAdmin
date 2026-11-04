using System.Net;
using System.Text;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Proves mapped BFF admission and late API expiry fail before any durable registration or audit survives.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationHttpFailureTests(BootstrapFixture fixture)
{
    private const string CreatedClient = "managed-http-client";
    private const string JsonType = "application/json";
    private const string Origin = "Origin";
    private const string CsrfHeader = "X-CSRF-TOKEN";
    private const string Invalid = "invalid-native-proof";
    private const int CreateOffsetMinutes = -20;
    private const int ExpirySeconds = 1;

    /// <summary>The newly mapped production BFF POST rejects absent and invalid native proof even with a genuine authenticated session.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealBffCreate_When_NativeAntiforgeryIsMissingOrInvalid_Then_NoManagedStateOrAuditIsWritten(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        foreach (var proof in new string?[] { null, Invalid })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApplicationHttpFixture.Collection)
            {
                Content = new StringContent(ApplicationHttpTests.Input(test.Now), Encoding.UTF8, JsonType)
            };
            request.Headers.Add(Origin, test.Environment.Profile.ClientUri.GetLeftPart(UriPartial.Authority));
            if (proof is not null)
            {
                request.Headers.Add(CsrfHeader, proof);
            }
            using var response = await test.Browser.SendAsync(request, TestContext.Current.CancellationToken);
            await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.BadRequest);
            Assert.Equal((0, 0), await ApplicationHttpTests.CountsAsync(test));
        }
    }

    /// <summary>Expiry after an actual native/policy save is rechecked inside the transaction; initial valid admission cannot authorize a late commit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_InitiallyValidHttpCreate_When_CredentialExpiresAfterSave_Then_NativeStateAndAuditRollBack(string provider)
    {
        var fault = new ApplicationWriteFault(ApplicationWriteDefect.CredentialExpired);
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider, fault);
        fault.Advance = () => test.Environment.Issuer.Account.Journey.Clock.Now = test.Now.AddSeconds(ExpirySeconds);
        fault.Armed = true;
        using var response = await test.CreateAsync(ApplicationHttpTests.Input(test.Now.AddMinutes(CreateOffsetMinutes).AddSeconds(ExpirySeconds)), direct: true);
        await ApplicationHttpTests.ProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(fault.Fired);
        await using var scope = test.Scope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreApplication>()
            .AnyAsync(row => row.ClientId == CreatedClient, TestContext.Current.CancellationToken));
        Assert.Equal((0, 0), await ApplicationHttpTests.CountsAsync(test));
    }
}
