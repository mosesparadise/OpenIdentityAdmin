using System.Diagnostics;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses real access tokens and independent API roots to reject committed current-authority changes without waiting for token expiry.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationAuthorityBrowserTests(BootstrapFixture fixture)
{
    private const string Callback = "/callback";
    private const int Success = 200;
    private const int Unauthorized = 401;
    private const int PropagationSeconds = 5;
    private const int RepeatedReads = 3;

    /// <summary>Each independent persisted defect is committed only after both services accepted the same genuine credential.</summary>
    [Theory]
    [InlineData("PostgreSql", ApplicationRevocation.Token, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationRevocation.Authorization, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationRevocation.SourceSession, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationRevocation.Account, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationRevocation.Profile, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationRevocation.Factor, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", ApplicationRevocation.SecurityStamp, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", ApplicationRevocation.Token, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationRevocation.Authorization, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationRevocation.SourceSession, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationRevocation.Account, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationRevocation.Profile, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationRevocation.Factor, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", ApplicationRevocation.SecurityStamp, Traits = ["Provider", "SqlServer"])]
    public Task Given_TwoWarmApis_When_CurrentAuthorityIsRevoked_Then_BothDenyWithinOriginalPropagationBound(string provider, ApplicationRevocation defect) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
            await using var browser = await BrowserSession.CreateAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, await browser.Context.NewPageAsync());
            await using var second = await ApplicationApiHost.CreateAsync(environment.Issuer.Account.Journey, provider, environment.Profile.Issuer,
                new Uri(environment.Profile.ClientUri, Callback).AbsoluteUri);
            Uri[] nodes = [environment.Api!.Address, second.Address];
            foreach (var node in nodes)
            {
                await ApplicationAccessFixture.RequireAsync(node, access.Value, Success);
            }
            var source = await BrowserHttpsState.CurrentSourceAsync(environment);
            await using (var scope = environment.Issuer.Account.CreateScope())
            {
                await ApplicationRevocationWriter.RevokeAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
                    environment.Issuer.Account.ActorId, source, defect);
            }
            var committed = Stopwatch.GetTimestamp();
            foreach (var node in nodes)
            {
                await ApplicationAccessFixture.RequireAsync(node, access.Value, Unauthorized);
            }
            Assert.True(Stopwatch.GetElapsedTime(committed) < TimeSpan.FromSeconds(PropagationSeconds));
            for (var index = 0; index < RepeatedReads; index++)
            {
                foreach (var node in nodes)
                {
                    await ApplicationAccessFixture.RequireAsync(node, access.Value, Unauthorized);
                }
            }
        });
}
