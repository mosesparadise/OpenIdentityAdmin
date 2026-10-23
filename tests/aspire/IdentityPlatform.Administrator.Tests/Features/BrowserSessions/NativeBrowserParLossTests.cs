using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Reuses the existing live response-start loss wrapper after committed PAR issuance; this is an in-process publication control, not TLS evidence.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserParLossTests(BootstrapFixture fixture)
{
    /// <summary>Loss before the first live body byte leaves exactly the actual attachment committed, and a fresh assertion cannot reissue it.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedNativeAttachment_When_OriginalLiveResponseStartIsLost_Then_CompletionRemainsSpentWithoutPublication(string provider)
    {
        var observed = new NativeBrowserParObservation();
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: observed.Configure);
        var before = await flow.RowAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        var loss = new PublicationLoss();
        observed.Armed = true;
        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(() => flow.PushAsync(configureHttp: loss.Install));
        }
        finally { observed.Armed = false; }
        Assert.Equal(1, loss.Reached);
        Assert.True(loss.EmptyAtStart);
        Assert.True(loss.Caused(failure));
        Assert.Equal(1, observed.Reached);
        var row = await flow.RowAsync();
        Assert.Equal(NativeBrowserParFixture.Attached, row.State);
        Assert.True(row.RequestId == observed.Identifier && row.SessionId == before.SessionId && row.CompletionDigest == before.CompletionDigest);
        Assert.Equal(before.Revision + 1, row.Revision);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(flow.Scenario, observed.Identifier));
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        await RequireAuditAsync(flow);
        var committed = await flow.AttachmentSnapshotAsync();
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync());
        Assert.True(committed == await flow.AttachmentSnapshotAsync());
        Assert.Equal(assertions + 2, await flow.AssertionsAsync());
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
    }

    /// <summary>Reads the correlated attachment audit after the failed transport has fully joined and disposed every owned scope.</summary>
    private static async Task RequireAuditAsync(NativeBrowserParFixture flow)
    {
        var row = await flow.RowAsync();
        await using var scope = flow.Journey.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>().AsNoTracking()
            .SingleAsync(value => value.Event == NativeBrowserParFixture.AttachEvent && value.CorrelationId == row.CorrelationId,
                TestContext.Current.CancellationToken);
        Assert.Equal(row.ObservedAt, audit.OccurredAt);
    }

    /// <summary>Installs the existing loss wrapper only on the original live feature before the production boundary installs captures.</summary>
    private sealed class PublicationLoss
    {
        private NativeLostResponse? loss;
        internal int Reached
        {
            get; private set;
        }
        internal bool EmptyAtStart
        {
            get; private set;
        }

        /// <summary>Reads the test-owned memory output synchronously at loss; no callback touches request services or output after request completion.</summary>
        internal void Install(HttpContext http)
        {
            var original = http.Features.Get<IHttpResponseBodyFeature>()!;
            loss = new NativeLostResponse(http, original, () =>
            {
                Reached++;
                EmptyAtStart = original.Stream.CanSeek && original.Stream.Length == 0;
            });
            http.Features.Set<IHttpResponseBodyFeature>(loss);
        }

        /// <summary>Requires this exact observed transport exception, never a cancellation or unrelated failure.</summary>
        internal bool Caused(Exception? error) => error is not null && loss?.Caused(error) == true;
    }
}
