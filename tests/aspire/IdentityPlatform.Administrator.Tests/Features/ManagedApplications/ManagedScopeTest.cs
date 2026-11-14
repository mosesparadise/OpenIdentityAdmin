using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Runs current policy component checks inside real owned transactions; this seam does not claim extended public protocol activation.</summary>
internal static class ManagedScopeTest
{
    internal static readonly string[] Names = ["email", "openid", "orders.read"];

    private const int Created = 201;
    private const int Updated = 200;
    private const string Email = "email";
    private const string Orders = "orders.read";
    private const string OpenId = "openid";

    /// <summary>Creates and assigns an actual managed native application through the audited administration operation.</summary>
    internal static async Task<string> SeedAsync(ApplicationMutationFixture test)
    {
        await ApplicationScopeEvidence.SeedAsync(test);
        var application = await test.ExecuteAsync(test.Create());
        Assert.Equal(Created, application.Status);
        var id = application.Application!.Id;
        Assert.Equal(Updated, (await test.ExecuteAsync(ApplicationMutationCommand.AssignScopes(id, application.Version!, new([Email, Orders]))!)).Status);
        return id;
    }

    /// <summary>Uses the production original request/child scope lifetime with an explicitly rollback-only policy read.</summary>
    internal static async Task<ManagedScopeDecision> CaptureAsync(ApplicationMutationFixture test, ManagedScopeDecision? expected = null)
    {
        var request = new ProtocolStorageRequest(test.Clock, test.Token);
        try
        {
            return await request.RunAsync(test.Api.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                if (expected is not null)
                {
                    await ManagedScopePolicy.VerifyAsync(scope, ApplicationMutationFixture.Client, expected, token);
                }
                return await ManagedScopePolicy.CaptureAsync(scope, ApplicationMutationFixture.Client, Names, [OpenId], token);
            });
        }
        finally { await request.DrainAsync(); }
    }
}
