using System.Net;
using System.Text.RegularExpressions;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Exercises fixed native security forms while keeping live synthetic secrets only in test memory.</summary>
internal static class SecurityHttp
{
    private const string MissingCodes = "The committed response did not contain exactly ten recovery codes.";
    private const string NonlocalContinuation = "The replacement continuation was not local.";
    internal const string Recovery = "/account/administrator/recovery";
    internal const string RecoveryVerify = "/account/administrator/recovery/verify";
    internal const string Replacement = "/account/administrator/security/replacement";
    internal const string Replace = "/account/administrator/security/replacement/verify";
    internal const string Confirm = "/account/administrator/security/replacement/confirm";
    internal const string Regeneration = "/account/administrator/security/recovery-codes";
    internal const string Regenerate = "/account/administrator/security/recovery-codes/verify";
    internal const string Logout = "/account/administrator/logout";

    /// <summary>Completes real enrollment and captures only the native secret values needed for subsequent explicit journeys.</summary>
    internal static async Task<SecurityEnrollment> EnrollAsync(AdministratorEnvironment environment, HttpClient browser)
    {
        var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var completed = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        return new SecurityEnrollment(form.Seed!, code, await CodesAsync(completed));
    }

    /// <summary>Parses the bounded expected native code response without including its HTML or values in assertion diagnostics.</summary>
    internal static async Task<string[]> CodesAsync(HttpResponseMessage response)
    {
        const string Pattern = "<code>([0-9A-F]{32})</code>";
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var matches = Regex.Matches(html, Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        const int ExpectedCodes = 10;
        Assert.True(matches.Count == ExpectedCodes, MissingCodes);
        return matches.Select(match => match.Groups[1].Value).ToArray();
    }

    /// <summary>Performs a bound step-up through a fixed route and requires an exact-origin redirect to replacement confirmation.</summary>
    internal static async Task<Uri> TransitionAsync(HttpClient browser, AdministratorForm form, string code, bool recovery)
    {
        using var response = await AdministratorJourney.ConfirmAtAsync(browser, form, code, recovery ? RecoveryVerify : Replace);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.True(location is { IsAbsoluteUri: false }, NonlocalContinuation);
        return location!;
    }
}

/// <summary>Holds synthetic live enrollment values without a generated secret-bearing record representation.</summary>
internal sealed class SecurityEnrollment(string seed, string acceptedCode, string[] recoveryCodes)
{
    internal string Seed { get; } = seed;
    internal string AcceptedCode { get; } = acceptedCode;
    internal string[] RecoveryCodes { get; } = recoveryCodes;
}
