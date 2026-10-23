using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires a real provider audit-INSERT failure after real native PAR creation, retaining only earlier assertion consumption.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserParAuditTests(BootstrapFixture fixture)
{
    private const string PostgresPermission = "42501";
    private const int SqlPermission = 229;

    /// <summary>Denied attachment audit rolls back the generated token and completion update; the spent assertion stays unusable while a new demand can succeed.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RealNativeTokenAndDeniedAuditInsert_When_AttachmentSaves_Then_OnlyEarlierAssertionConsumptionRemains(string provider)
    {
        var observed = new NativeBrowserParObservation();
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: observed.Configure);
        var original = await flow.RowAsync();
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        var assertion = flow.Scenario.Material.Sign(flow.Journey.Clock.Now.ToUnixTimeSeconds(), audience: flow.Scenario.Issuer.AbsoluteUri);
        await DenyAuditAsync(flow, observed, assertion);
        Assert.Equal(1, observed.Reached);
        Assert.Null(await NativeAccountPersistence.StatusAsync(flow.Scenario, observed.Identifier));
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        NativeAccountDenialTests.RequireJson(await flow.PushAsync(assertion: assertion), OpenIddictConstants.Errors.InvalidClient);
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        _ = await flow.RequireAttachedAsync(await flow.PushAsync(), original, flow.Journey.Clock.Now.ToUnixTimeSeconds());
        Assert.True((await flow.RowAsync()).RequestId != observed.Identifier);
        Assert.Equal(assertions + 2, await flow.AssertionsAsync());
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
    }

    /// <summary>Restores the existing runtime permission only after the one complete native invocation and cleanup have returned.</summary>
    private static async Task DenyAuditAsync(NativeBrowserParFixture flow, NativeBrowserParObservation observed, string assertion)
    {
        await IssuerAuditPermission.RunAsync(flow.Journey.Lease, async () =>
        {
            observed.Armed = true;
            try
            {
                var response = await flow.PushAsync(assertion: assertion);
                RequirePermissionFailure(response);
            }
            finally { observed.Armed = false; }
        });
    }

    /// <summary>Requires the actual provider permission error rather than treating an arbitrary ownership failure as audit atomicity evidence.</summary>
    private static void RequirePermissionFailure(NativeAccountResult response)
    {
        Assert.False(response.Claimed);
        Assert.False(response.Aborted);
        Assert.False(response.HasCookie);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.True(response.Body.Length == 0 && response.Location.Length == 0);
        var failure = Assert.IsType<DbUpdateException>(response.Failure);
        Assert.True(failure.InnerException is Npgsql.PostgresException { SqlState: PostgresPermission } ||
            failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlPermission });
    }
}
