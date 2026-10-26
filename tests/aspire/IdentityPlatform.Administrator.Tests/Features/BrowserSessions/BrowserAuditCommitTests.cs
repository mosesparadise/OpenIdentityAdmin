using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Distinguishes durable audit linkage from uncertain acknowledgement at the actual native commit boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserAuditCommitTests(BootstrapFixture fixture)
{
    /// <summary>Unknown commit suppresses publication and leaves one durable link; a new assertion cannot reattach the spent completion.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RealNativeAttachment_When_CommitAcknowledgementIsLost_Then_OneLinkRemainsWithoutReplay(string provider)
    {
        var loss = new BrowserAuditCommitLoss();
        var observed = new NativeBrowserParObservation { AfterPersisted = loss.Arm };
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: services =>
        {
            observed.Configure(services);
            services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(loss));
        });
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        observed.Armed = true;
        var response = await flow.PushAsync();
        observed.Armed = false;
        Assert.Same(loss.Failure, response.Failure);
        Assert.Equal(1, observed.Reached);
        Assert.Equal(1, loss.Commits);
        Assert.False(response.HasCookie);
        Assert.False(response.Claimed);
        Assert.False(response.Aborted);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.True(response.Body.Length == 0 && response.Location.Length == 0);
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
        await flow.RequireLinkAsync();
        var committed = await flow.AttachmentSnapshotAsync();
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync());
        Assert.True(committed == await flow.AttachmentSnapshotAsync());
        Assert.Equal(1, loss.Commits);
    }
}
