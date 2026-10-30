using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves real managed password/MFA shares native account budgets and replay protection without administrator completion.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedHumanBudgetTests(BootstrapFixture fixture)
{
    private const string WrongPassword = "synthetic-wrong-password";
    private const string ExpectedFormPurpose = "IdentityPlatform.ManagedApplications.Form.v1";
    private const string Login = "Login";

    /// <summary>Wrong password and reused administrator OTP increment the same budget; successful password alone never resets it, and generic completion cannot consume the managed ceremony.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ExistingAdministratorProof_When_ManagedAuthenticationRuns_Then_FreshProofAndSharedBudgetRemainRequired(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var form = await ManagedHumanHttp.StartAsync(test);
        using var wrong = await ManagedHumanHttp.PasswordAsync(test, form, WrongPassword);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(1, await ManagedHumanEvidence.FailuresAsync(test));
        using var password = await ManagedHumanHttp.PasswordAsync(test, form);
        await ManagedHumanHttp.EntryAsync(password);
        Assert.Equal(1, await ManagedHumanEvidence.FailuresAsync(test));
        var mfa = await ManagedHumanHttp.FormAsync(test.Http.IssuerClient, password.Headers.Location!);
        var journey = test.Http.Environment.Issuer.Account.Journey;
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var replay = await database.Set<AdministratorReplay>().AsNoTracking().OrderByDescending(row => row.AcceptedAt).FirstAsync(TestContext.Current.CancellationToken);
        var used = journey.Get<FactorProtection>().Unprotect(replay.ProtectedCode, replay.UserId, replay.Generation, FactorProtection.Replay);
        using var denied = await ManagedHumanHttp.MfaAsync(test, mfa, used);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(2, await ManagedHumanEvidence.FailuresAsync(test));
        var row = Assert.Single((await test.SnapshotAsync()).Interactions);
        var ceremony = await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(value => value.Id == row.CeremonyId, TestContext.Current.CancellationToken);
        var fresh = await IssuerContinuationProviderFixture.UnusedCodeAsync(journey);
        var original = JsonSerializer.Deserialize<ManagedHumanRequest>(test.Http.Environment.IssuerServices
            .GetRequiredService<IDataProtectionProvider>().CreateProtector(ExpectedFormPurpose).Unprotect(mfa[ManagedHumanHttp.Flow]))!;
        Assert.Null(await journey.Get<JourneyCompletion>().CompleteAsync(ceremony.PublicId, original.Correlation, Login, fresh, TestContext.Current.CancellationToken));
        using var accepted = await ManagedHumanHttp.MfaAsync(test, mfa, fresh);
        await ManagedHumanHttp.EntryAsync(accepted);
        Assert.Equal(0, await ManagedHumanEvidence.FailuresAsync(test));
    }
}
