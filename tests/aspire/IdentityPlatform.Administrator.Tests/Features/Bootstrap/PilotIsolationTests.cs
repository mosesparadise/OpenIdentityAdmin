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
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Native pilot isolation from pending administrator initialization.</summary>
[Collection("administrator-bootstrap")]
public sealed class BootstrapPilotIsolationTests
{
    private const string FailedPilotControl = "Genuine pilot control did not validate.";
    private const int SyntheticPasswordBytes = 24;
    private const string OperationIdFormat = "D";
    private const string SyntheticOperator = "synthetic-operator";
    private const string DisclosedBootstrapInput = "Bootstrap disclosed input.";
    private const string LoginPath = "/account/login";
    private const string UnexpectedAdministratorCallback = "Pending administrator reached the callback.";
    private const string ChangedPilotManifest = "Pilot manifest changed.";
    private const string PasswordVerificationEvent = "password-verification";
    private const string DeniedOutcome = "denied";
    private const string AnonymousActor = "anonymous";

    /// <summary>Proves the genuine pilot succeeds while a newly initialized pending administrator receives no authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealPilotAndPendingAdministrator_When_EachSignsIn_Then_OnlyPilotReceivesAuthority(string provider)
    {
        await using var environment = await IssuerEnvironment.CreateAsync(provider, Token);
        using (var pilot = IssuerEnvironment.CreateBrowserClient())
        {
            var flow = await environment.AuthorizeAsync(pilot, exchange: true);
            Assert.True(flow.Validated, FailedPilotControl);
        }
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(SyntheticPasswordBytes));
        const string username = "pending.administrator";
        await InitializePendingAsync(environment, provider, username, password);
        await RequireDeniedAsync(environment, username, password);
    }

    /// <summary>Initializes the pending administrator through the real runtime-credential command.</summary>
    private static async Task InitializePendingAsync(IssuerEnvironment environment, string provider, string username, string password)
    {
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
                operationId = Guid.NewGuid().ToString(OperationIdFormat),
                operatorReference = SyntheticOperator,
                userName = username,
                password
            }));
            var result = await process.CompleteAsync();
            Assert.Equal(0, result.ExitCode);
            Assert.False(result.Output.Contains(password, StringComparison.Ordinal), DisclosedBootstrapInput);
        }
    }

    /// <summary>Requires denial, unchanged request-relative authority and unchanged pilot/account state with exact anonymous audit.</summary>
    private static async Task RequireDeniedAsync(IssuerEnvironment environment, string username, string password)
    {
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
        Assert.Equal(LoginPath, response.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.False(login.Flow.Completion.Task.IsCompletedSuccessfully, UnexpectedAdministratorCallback);
        Assert.Equal(sessionCount, await database.Set<LocalIdentitySession>().CountAsync(Token));
        Assert.Empty(await database.Set<LocalIdentitySession>().Where(value => value.UserId == user.Id).ToListAsync(Token));
        Assert.Equal(tokens, await database.Set<OpenIddictEntityFrameworkCoreToken>().Select(value => value.Id).OrderBy(value => value).ToListAsync(Token));
        Assert.True(manifest == JsonSerializer.Serialize(await database.Set<PilotManifest>().AsNoTracking().SingleAsync(Token)), ChangedPilotManifest);
        var loginAudits = await database.Set<SecurityAuditIntent>().Where(value => value.TargetId == user.Id && value.Event == PasswordVerificationEvent).ToListAsync(Token);
        Assert.Single(loginAudits);
        Assert.Equal(DeniedOutcome, loginAudits[0].Outcome);
        Assert.Equal(AnonymousActor, loginAudits[0].ActorType);
        Assert.False((await database.Users.AsNoTracking().SingleAsync(value => value.Id == user.Id, Token)).IsEnabled);
    }
}
