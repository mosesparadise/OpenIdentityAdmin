using System.Net;
using System.Text.Json;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Host.Tests.Features.Pilot;

public sealed class CriticalDecisionAssertionTests
{
    private const string Marker = "Critical decision bypass: test-control.";
    private const string Envelope = "{\"access_token\":\"synthetic-access\",\"id_token\":\"synthetic-id\",\"token_type\":\"Bearer\"}";

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Unrelated_status_cannot_be_a_token_bypass(int status)
    {
        using var response = Response((HttpStatusCode)status);
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.DeniedTokensAsync(response, Valid, Marker));
        Assert.DoesNotContain(Marker, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"access_token\":\"a\",\"id_token\":\"b\",\"token_type\":\"wrong\"}")]
    public async Task Malformed_success_cannot_be_a_token_bypass(string body)
    {
        using var response = Response(HttpStatusCode.OK, body);
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.DeniedTokensAsync(response, Valid, Marker));
        Assert.DoesNotContain(Marker, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failed_independent_validation_cannot_be_a_token_bypass()
    {
        using var response = Response(HttpStatusCode.OK, Envelope);
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.DeniedTokensAsync(response, _ => Task.FromResult(false), Marker));
        Assert.DoesNotContain(Marker, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_cannot_be_a_token_bypass()
    {
        using var response = Response(HttpStatusCode.OK, Envelope);
        await Assert.ThrowsAsync<OperationCanceledException>(() => CriticalDecisionAssertions.DeniedTokensAsync(response, _ => throw new OperationCanceledException(), Marker));
    }

    [Fact]
    public async Task Validated_token_success_emits_exact_marker_and_denial_never_calls_validator()
    {
        using var success = Response(HttpStatusCode.OK, Envelope);
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.DeniedTokensAsync(success, Valid, Marker));
        Assert.Equal(Marker, error.Message);
        using var denied = Response(HttpStatusCode.BadRequest);
        await CriticalDecisionAssertions.DeniedTokensAsync(denied, _ => throw new InvalidOperationException(), Marker);
    }

    [Theory]
    [InlineData(400, 400)]
    [InlineData(503, 503)]
    [InlineData(200, 503)]
    public async Task Zero_winners_or_unrelated_failure_cannot_be_a_replay_bypass(int first, int second)
    {
        using var one = Response((HttpStatusCode)first, Envelope);
        using var two = Response((HttpStatusCode)second, Envelope);
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.SingleWinnerAsync([one, two], Valid, Marker));
        Assert.DoesNotContain(Marker, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replay_requires_both_successes_to_validate()
    {
        using var one = Response(HttpStatusCode.OK, Envelope);
        using var two = Response(HttpStatusCode.OK, "{}");
        var error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.SingleWinnerAsync([one, two], Valid, Marker));
        Assert.DoesNotContain(Marker, error.Message, StringComparison.Ordinal);
        two.Content = new StringContent(Envelope);
        var calls = 0;
        error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.SingleWinnerAsync([one, two], _ => Task.FromResult(++calls == 1), Marker));
        Assert.DoesNotContain(Marker, error.Message, StringComparison.Ordinal);
        Assert.Equal(2, calls);
        error = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => CriticalDecisionAssertions.SingleWinnerAsync([one, two], Valid, Marker));
        Assert.Equal(Marker, error.Message);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body = "") => new(status) { Content = new StringContent(body) };
    private static Task<bool> Valid(JsonElement _) => Task.FromResult(true);
}
