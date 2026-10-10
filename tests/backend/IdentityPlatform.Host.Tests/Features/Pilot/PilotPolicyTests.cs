using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.Pilot;

public sealed class PilotPolicyTests
{
    [Theory]
    [InlineData(42, false)]
    [InlineData(43, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Verifier_requires_RFC7636_length(int length, bool expected)
        => Assert.Equal(expected, ProofKeyPolicy.IsVerifier(new string('a', length)));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789-._~ABCDEFG", true)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789+/ABCDEFG", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789éABCDEFG", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789 ABCDEFG", false)]
    public void Verifier_accepts_only_unreserved_ASCII(string? verifier, bool expected)
        => Assert.Equal(expected, ProofKeyPolicy.IsVerifier(verifier));

    [Fact]
    public void Challenge_requires_canonical_SHA256_base64url()
    {
        Assert.True(ProofKeyPolicy.IsChallenge(new string('A', 43)));
        Assert.False(ProofKeyPolicy.IsChallenge(new string('A', 42) + "B"));
        Assert.False(ProofKeyPolicy.IsChallenge(new string('A', 44)));
        Assert.False(ProofKeyPolicy.IsChallenge(null));
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("60", 60L)]
    [InlineData("00060", 60L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void Maximum_age_preserves_valid_nonnegative_integer(string raw, long expected)
    {
        var request = new OpenIddictRequest(new Dictionary<string, string?> { ["max_age"] = raw });
        Assert.True(AuthenticationFreshness.TryGetMaximumAge(request, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("١")]
    [InlineData("9223372036854775808")]
    public void Malformed_maximum_age_is_not_treated_as_absent(string raw)
    {
        var request = new OpenIddictRequest(new Dictionary<string, string?> { ["max_age"] = raw });
        Assert.False(AuthenticationFreshness.TryGetMaximumAge(request, out _));
    }

    [Fact]
    public void Absent_age_and_exact_positive_boundary_preserve_original_authentication_time()
    {
        var request = new OpenIddictRequest();
        Assert.True(AuthenticationFreshness.TryGetMaximumAge(request, out var age));
        Assert.Null(age);
        Assert.False(AuthenticationFreshness.Required(request, null, 1000, 5000));
        Assert.False(AuthenticationFreshness.Required(request, 60, 1000, 1060));
        Assert.True(AuthenticationFreshness.Required(request, 60, 1000, 1061));
        Assert.True(AuthenticationFreshness.Required(request, 0, 1000, 1000));
        request.Prompt = OpenIddictConstants.PromptValues.Login;
        Assert.True(AuthenticationFreshness.Required(request, null, 1000, 1000));
    }

    [Fact]
    public void Browser_secrets_are_256_bit_and_checked_against_independent_digest()
    {
        var value = InteractionSecrets.Create();
        Assert.True(InteractionSecrets.IsIdentifier(value));
        Assert.Equal(32, WebEncoders.Base64UrlDecode(value).Length);
        Assert.True(InteractionSecrets.Matches(value, InteractionSecrets.Digest(value)));
        Assert.False(InteractionSecrets.Matches(InteractionSecrets.Create(), InteractionSecrets.Digest(value)));
        Assert.False(InteractionSecrets.Matches(value, "broken"));
        Assert.False(InteractionSecrets.Matches(value, InteractionSecrets.Digest(value).ToLowerInvariant()));
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", InteractionSecrets.Digest("abc"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa=")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaé")]
    public void Invalid_identifiers_cannot_bind(string? value)
    {
        Assert.False(InteractionSecrets.IsIdentifier(value));
        Assert.False(InteractionSecrets.Matches(value, InteractionSecrets.Digest("abc")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("FaLsE")]
    public void Default_profile_stays_health_only(string? value)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Pilot:Enabled"] = value }).Build();
        Assert.False(PilotConfiguration.IsEnabled(settings, new TestEnvironment("Production"), null));
    }

    [Theory]
    [InlineData("true", "Development")]
    [InlineData("true", "Production")]
    [InlineData("true", "Staging")]
    [InlineData("", "Development")]
    [InlineData("1", "Development")]
    [InlineData(" true ", "Development")]
    public void Invalid_or_unprotected_profile_rejects_with_fixed_safe_error(string value, string environment)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Pilot:Enabled"] = value }).Build();
        var error = Assert.Throws<PilotConfigurationException>(() => PilotConfiguration.IsEnabled(settings, new TestEnvironment(environment), null));
        Assert.Equal(new PilotConfigurationException().Message, error.Message);
    }

    [Theory]
    [InlineData("{}", false)]
    [InlineData("{\"profile\":\"first-party-v1\",\"enabled\":true}", true)]
    [InlineData("{\"profile\":\"first-party-v1\",\"enabled\":false}", false)]
    [InlineData("{\"profile\":\"FIRST-PARTY-V1\",\"enabled\":true}", false)]
    [InlineData("{\"profile\":\"first-party-v1\",\"enabled\":\"true\"}", false)]
    [InlineData("{\"profile\":null,\"enabled\":true}", false)]
    [InlineData("{\"profile\":1,\"enabled\":true}", false)]
    public void Only_exact_enabled_profile_is_eligible(string json, bool expected)
    {
        Assert.Equal(expected, PilotEligibility.Enabled(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!));
    }

    [Fact]
    public void Principal_has_deliberate_destinations_and_no_credential_or_role_claims()
    {
        var session = new LocalIdentitySession { Id = InteractionSecrets.Create(), UserId = "synthetic-subject", SecurityStamp = "stamp-sentinel", AuthenticatedAt = 1000, ExpiresAt = 1900 };
        var principal = PilotEligibility.Principal(session);
        Assert.Equal(session.UserId, principal.GetClaim(OpenIddictConstants.Claims.Subject));
        Assert.Equal("1000", principal.GetClaim(OpenIddictConstants.Claims.AuthenticationTime));
        Assert.Equal(session.Id, principal.GetClaim(PilotProfile.SessionClaim));
        Assert.Equal(["openid", "pilot_api"], principal.GetScopes());
        Assert.Equal(["identityplatform-pilot-api"], principal.GetResources());
        Assert.Equal(["access_token", "id_token"], principal.FindFirst("sub")!.GetDestinations());
        Assert.Equal(["id_token"], principal.FindFirst("auth_time")!.GetDestinations());
        Assert.Empty(principal.FindFirst(PilotProfile.SessionClaim)!.GetDestinations());
        Assert.True(!principal.Claims.Any(claim => claim.Value == session.SecurityStamp || claim.Type.Contains("role", StringComparison.OrdinalIgnoreCase)),
            "The principal must not carry credential or role claims.");
        Assert.Equal(2, principal.Claims.Count(claim => !claim.Type.StartsWith("oi_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Resume_is_fixed_local_and_encodes_stored_values_as_data()
    {
        var interaction = new LoginInteraction { Id = InteractionSecrets.Create(), RequestTokenId = "token", RequestUri = "urn:request:a&redirect_uri=https://evil.example/#x", ClientId = "client&extra=value", BrowserDigest = "digest" };
        var value = LoginInteractions.Resume(interaction);
        Assert.StartsWith("/connect/authorize?", value);
        var query = QueryHelpers.ParseQuery(value[value.IndexOf('?', StringComparison.Ordinal)..]);
        Assert.Equal(2, query.Count);
        Assert.Equal(interaction.ClientId, query["client_id"]);
        Assert.Equal(interaction.RequestUri, query["request_uri"]);
        var cookie = LoginInteractions.CookieOptions();
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal("/", cookie.Path);
        Assert.Null(cookie.Domain);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal(TimeSpan.FromMinutes(5), cookie.MaxAge);
    }

    [Fact]
    public async Task Excess_correlation_cookies_reject_before_any_database_access_or_cleanup_headers()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = string.Join("; ", Enumerable.Range(0, 9).Select(_ => PilotProfile.CorrelationPrefix + InteractionSecrets.Create() + "=" + InteractionSecrets.Create()));
        var interactions = new LoginInteractions(null!, null!, null!, null!, null!, TimeProvider.System);
        Assert.Null(await interactions.CreateAsync(context, "token", "urn:request:synthetic", long.MaxValue));
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PolicyTests";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
