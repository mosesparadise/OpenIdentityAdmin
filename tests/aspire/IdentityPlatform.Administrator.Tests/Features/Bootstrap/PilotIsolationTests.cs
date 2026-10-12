using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

public sealed partial class BootstrapTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Real_pilot_allows_its_user_and_denies_pending_administrator_without_issuing_authority(string provider)
    {
        await using var environment = await IssuerEnvironment.CreateAsync(provider, Token);
        using (var pilot = IssuerEnvironment.CreateBrowserClient())
        {
            var flow = await environment.AuthorizeAsync(pilot, exchange: true);
            Assert.True(flow.Validated, "Genuine pilot control did not validate.");
        }
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        const string username = "pending.administrator";
        string connection;
        await using (var runtime = environment.RuntimeServices())
        await using (var scope = runtime.CreateAsyncScope())
        {
            connection = scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.GetConnectionString()!;
        }
        await using (var process = new BootstrapProcess(provider, connection))
        {
            await process.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new
            {
                operationId = Guid.NewGuid().ToString("D"),
                operatorReference = "synthetic-operator",
                userName = username,
                password
            }));
            var result = await process.CompleteAsync();
            Assert.Equal(0, result.ExitCode);
            Assert.False(result.Output.Contains(password, StringComparison.Ordinal), "Bootstrap disclosed input.");
        }
        await using var inspect = environment.Services.CreateAsyncScope();
        var database = inspect.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await database.Users.AsNoTracking().SingleAsync(value => value.UserName == username, Token);
        var manifest = JsonSerializer.Serialize(await database.Set<PilotManifest>().AsNoTracking().SingleAsync(Token));
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var login = await TestLogin.BeginAsync(environment, browser, exchange: true);
        // Baseline after starting the legitimate request excludes its request token from the denial comparison.
        var sessionCount = await database.Set<LocalIdentitySession>().CountAsync(Token);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().Select(value => value.Id).OrderBy(value => value).ToListAsync(Token);
        using var response = await login.SubmitAsync(environment, browser, username: username, password: password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/account/login", response.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.False(login.Flow.Completion.Task.IsCompletedSuccessfully, "Pending administrator reached the callback.");
        Assert.Equal(sessionCount, await database.Set<LocalIdentitySession>().CountAsync(Token));
        Assert.Empty(await database.Set<LocalIdentitySession>().Where(value => value.UserId == user.Id).ToListAsync(Token));
        Assert.Equal(tokens, await database.Set<OpenIddictEntityFrameworkCoreToken>().Select(value => value.Id).OrderBy(value => value).ToListAsync(Token));
        Assert.True(manifest == JsonSerializer.Serialize(await database.Set<PilotManifest>().AsNoTracking().SingleAsync(Token)), "Pilot manifest changed.");
        var loginAudits = await database.Set<SecurityAuditIntent>().Where(value => value.TargetId == user.Id && value.Event == "password-verification").ToListAsync(Token);
        Assert.Single(loginAudits);
        Assert.Equal("denied", loginAudits[0].Outcome);
        Assert.Equal("anonymous", loginAudits[0].ActorType);
        Assert.False((await database.Users.AsNoTracking().SingleAsync(value => value.Id == user.Id, Token)).IsEnabled);
    }
}
