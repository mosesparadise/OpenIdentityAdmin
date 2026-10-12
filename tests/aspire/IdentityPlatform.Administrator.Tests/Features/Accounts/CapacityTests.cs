using System.Security.Cryptography;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves session admission precedes any factor reservation or activation mutation.</summary>
[Collection("administrator-bootstrap")]
public sealed class CapacityTests(BootstrapFixture fixture)
{
    private const string Enrollment = "Enrollment";
    private const string Methods = "pwd otp";
    private const int LiveLimit = 8;
    private const int SessionSeconds = 28800;
    private const int IdentifierBytes = 32;

    /// <summary>A full live-session budget leaves the exact bound OTP unspent and becomes usable once legitimate capacity is released.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FullSessionCapacity_When_EnrollmentProofArrives_Then_FactorAndInteractionRemainUnspent(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        var token = TestContext.Current.CancellationToken;
        var user = await scenario.Database.Users.SingleAsync(token);
        var now = scenario.Clock.Now.ToUnixTimeSeconds();
        for (var index = 0; index < LiveLimit; index++)
        {
            scenario.Database.Add(new AdministratorSession
            {
                PublicId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(IdentifierBytes)),
                UserId = user.Id,
                SecurityStamp = user.SecurityStamp!,
                AuthenticationMethods = Methods,
                PasswordVerifiedAt = now,
                FactorVerifiedAt = now,
                CreatedAt = now,
                LastActivityAt = now,
                ExpiresAt = now + SessionSeconds
            });
        }
        await scenario.Database.SaveChangesAsync(token);
        var code = FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Null(await scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction, scenario.Correlation, Enrollment, code, token));
        Assert.Empty(await scenario.Database.Set<AdministratorReplay>().ToArrayAsync(token));
        Assert.Empty(await scenario.Database.Set<AdministratorFactor>().ToArrayAsync(token));
        Assert.Equal(LiveLimit, await scenario.Database.Set<AdministratorSession>().CountAsync(token));
        await scenario.Database.Set<AdministratorSession>().ExecuteDeleteAsync(token);
        Assert.NotNull(await scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction, scenario.Correlation, Enrollment, code, token));
    }
}
