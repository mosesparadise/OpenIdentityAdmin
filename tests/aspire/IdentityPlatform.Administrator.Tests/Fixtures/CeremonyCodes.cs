using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Establishes native-valid unused inputs so replay or expiry cannot mask negative policy assertions.</summary>
internal static class CeremonyCodes
{
    private const int StepSeconds = 30;
    private const string MissingCandidate = "No distinct native-window test candidate.";

    /// <summary>Proves a near-center independent code is valid, distinct and sampled within one native counter transition.</summary>
    internal static async Task<string> UnusedAsync(FactorScenario factors, string previous, Guid? staged = null, string? excluded = null)
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int[] offsets = [0, 1, -1];
        var code = offsets.Select(offset => FactorOracle.Code(before + offset * StepSeconds))
            .FirstOrDefault(candidate => candidate != previous && candidate != excluded);
        Assert.True(code is not null, MissingCandidate);
        if (staged is { } interaction)
        {
            Assert.NotNull(await VerifiedStagedFactor.VerifyAsync(factors.Database, factors.Access, factors.Users,
                TimeProvider.System, interaction, code, factors.Token));
        }
        else
        {
            var user = await factors.Users.FindByIdAsync(factors.User.Id);
            Assert.True(await factors.Users.VerifyTwoFactorTokenAsync(user!, TokenOptions.DefaultAuthenticatorProvider, code));
        }
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Assert.InRange(after / StepSeconds - before / StepSeconds, 0, 1);
        return code;
    }
}
