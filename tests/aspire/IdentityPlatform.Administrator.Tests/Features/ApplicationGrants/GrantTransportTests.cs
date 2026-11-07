using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using static IdentityPlatform.Administrator.Tests.Features.ApplicationGrants.GrantTransportContext;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Exercises grant-specific strict parsing, status contracts and secrecy through genuine BFF and independently authorized TLS API routes.</summary>
[Collection("administrator-bootstrap")]
public sealed class GrantTransportTests(BootstrapFixture fixture)
{
    private const string Job = "job";
    private const string Version = "version";
    private const string ApplicationId = "applicationId";
    private const string Id = "id";
    private const string Separator = "/";
    private const string Suffix = "/grant-revocations/";
    private const string Expires = "expiresAt";
    private const string Expired = "expired";
    private static readonly int[] CommonErrors = [400, 401, 403, 404];
    private static readonly int[] MutationErrors = [409, 412, 428];

    /// <summary>All four routes preserve exact original intent and suppress hostile response data without retrying or altering durable accepted work.</summary>
    [Theory]
    [InlineData("PostgreSql", Create, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Continue, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Exact, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", Recent, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Create, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Continue, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Exact, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", Recent, Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineGrantOperation_When_DownstreamResponsesViolateContracts_Then_OnlySanitizedFiniteResultsReachBrowser(string provider, string shape)
    {
        await using var test = await CreateAsync(fixture, provider, shape);
        using (var response = await test.SendAsync())
        {
            await SuccessAsync(test, response, shape);
        }
        var before = await test.SnapshotAsync();
        foreach (var fault in GrantTransportFaults.For(shape))
        {
            await RejectAsync(test, fault, fault == Expired ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable);
        }
        foreach (var status in GrantTransportFaults.Statuses)
        {
            var expected = CommonErrors.Contains(status) || test.Mutation && MutationErrors.Contains(status)
                ? (HttpStatusCode)status : HttpStatusCode.ServiceUnavailable;
            await RejectAsync(test, status.ToString(CultureInfo.InvariantCulture), expected);
        }
        foreach (var control in new[] { GrantTransportFaults.LaterExpiry, GrantTransportFaults.LargeCounts, GrantTransportFaults.ExactBytes })
        {
            test.Probe.Fault = control;
            using var response = await test.SendAsync();
            await SuccessAsync(test, response, shape);
        }
        Assert.Equal(before, await test.SnapshotAsync());
        Assert.Equal(test.Probe.Requests, test.Probe.Responses);
        Assert.Equal(0, test.Probe.Redirects);
        await test.RequireRecoveryAsync();
    }

    /// <summary>Requires one actual successful API operation followed by the exact sanitized BFF error, excluding earlier harness/authentication failures.</summary>
    private static async Task RejectAsync(GrantTransportContext test, string fault, HttpStatusCode status)
    {
        test.Probe.Fault = fault;
        var requests = test.Probe.Requests;
        using var response = await test.SendAsync();
        Assert.Equal(requests + 1, test.Probe.Requests);
        Assert.Equal(test.Probe.Requests, test.Probe.Responses);
        await ApplicationHttpTests.ProblemAsync(response, status);
        await SafeAsync(response);
    }

    /// <summary>Compares every safe field to the genuine API result and independently bounds all published expiries to original server-held authority.</summary>
    private static async Task SuccessAsync(GrantTransportContext test, HttpResponseMessage response, string shape)
    {
        Assert.Equal(test.Mutation ? HttpStatusCode.Accepted : HttpStatusCode.OK, response.StatusCode);
        var output = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var original = JsonNode.Parse(test.Probe.WireBody)!;
        Normalize(output, test.Test.AccessExpiresAt);
        Normalize(original, DateTimeOffset.MaxValue);
        Assert.True(JsonNode.DeepEquals(original, output));
        var job = output[Job] ?? output;
        if (shape != Recent)
        {
            Assert.Equal(job[Version]!.GetValue<string>(), response.Headers.ETag!.Tag);
        }
        else
        {
            Assert.Null(response.Headers.ETag);
        }
        if (test.Mutation)
        {
            Assert.Equal(ApplicationHttpFixture.Collection + Separator + job[ApplicationId]!.GetValue<string>() + Suffix + job[Id]!.GetValue<string>(),
                response.Headers.Location!.OriginalString);
        }
        else
        {
            Assert.Null(response.Headers.Location);
        }
        await SafeAsync(response);
    }

    /// <summary>Checks every nested expiry before removing only that observation field for a full independent payload comparison.</summary>
    private static void Normalize(JsonNode node, DateTimeOffset maximum)
    {
        if (node is JsonObject value)
        {
            if (value[Expires] is { } expiry)
            {
                Assert.True(expiry.GetValue<DateTimeOffset>() <= maximum);
                value.Remove(Expires);
            }
            foreach (var child in value)
            {
                if (child.Value is { } nested)
                {
                    Normalize(nested, maximum);
                }
            }
        }
        else if (node is JsonArray items)
        {
            foreach (var item in items)
            {
                Normalize(item!, maximum);
            }
        }
    }

    /// <summary>Rejects upstream cookies/private metadata and canary bodies on success and failure alike.</summary>
    internal static async Task SafeAsync(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.False(response.Headers.Contains(GrantTransportProbe.CookieHeader));
        Assert.False(response.Headers.Contains(GrantTransportProbe.PrivateHeader));
        Assert.DoesNotContain(GrantTransportProbe.Canary, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
