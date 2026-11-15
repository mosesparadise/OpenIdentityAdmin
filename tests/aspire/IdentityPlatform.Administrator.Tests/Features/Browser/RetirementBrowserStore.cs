using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks permanent receipt identity and retained native state independently of browser response handling.</summary>
internal static class RetirementBrowserStore
{
    /// <summary>Reads the immutable receipt through an unrelated scope after actual browser admission.</summary>
    internal static async Task<ApplicationRetirement> ReceiptAsync(BrowserHttpsFixture environment, string application)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<ApplicationRetirement>().AsNoTracking()
            .SingleAsync(row => row.ApplicationId == application, TestContext.Current.CancellationToken);
    }

    /// <summary>Requires the original receipt, one terminal revision and native identity to survive logout, all batches and continuation.</summary>
    internal static async Task RequireAsync(BrowserHttpsFixture environment, ApplicationRetirement receipt, ApplicationUpdateSnapshot before)
    {
        var current = await ReceiptAsync(environment, receipt.ApplicationId);
        Assert.Equal(ApplicationUpdateSnapshot.Hash(receipt), ApplicationUpdateSnapshot.Hash(current));
        var configuration = await GrantRecoveryBrowserStore.ConfigurationAsync(environment, receipt.ApplicationId);
        Assert.True(configuration.Policy.Retired);
        Assert.False(configuration.Policy.Enabled);
        Assert.Equal(before.Policy.Revision + 1, configuration.Policy.Revision);
        Assert.Equal(receipt.RetiredRevision, configuration.Policy.Revision);
        Assert.Equal(before.NativeDigest, configuration.NativeDigest);
        var job = await GrantRecoveryBrowserStore.ReadAsync(environment, receipt.ApplicationId);
        Assert.Equal(receipt.GrantJobId, job.Job.Id);
        Assert.Contains(job.Approvals, row => row.Id == receipt.GrantApprovalId);
    }
}
