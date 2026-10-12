using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Holds the actual shared lock until both real production requests are observed waiting.</summary>
internal static class JourneyContention
{
    private const string GovernanceStage = "%UPDATE%AdministratorGovernance%";
    private const string AdmissionStage = "%UPDATE%AdministratorAdmission%";
    private const int CompetingRequests = 2;
    private const int DrainSeconds = 10;

    /// <summary>Starts independent form submissions and releases their blocker only after provider-native wait-graph observation.</summary>
    internal static async Task<HttpResponseMessage[]> CompleteAsync(AdministratorEnvironment environment, string provider,
        HttpClient primary, HttpClient secondary, AdministratorForm first, AdministratorForm second, string code, bool enrollment)
    {
        await using var services = new FactorServices(provider, environment.Database, environment.Material);
        await using var scope = services.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Task<HttpResponseMessage[]>? completion = null;
        try
        {
            await using (var transaction = await CeremonyTransaction.OpenAsync(database, scope.ServiceProvider.GetRequiredService<NativeOperation>(),
                new(enrollment, !enrollment, [], []), TestContext.Current.CancellationToken))
            {
                var blocker = await DatabaseWaiters.IdentifyAsync(database, TestContext.Current.CancellationToken);
                completion = Task.WhenAll(AdministratorJourney.ConfirmAsync(primary, first, code, enrollment),
                    AdministratorJourney.ConfirmAsync(secondary, second, code, enrollment));
                await DatabaseWaiters.ObserveAsync(environment.Database, blocker, enrollment ? GovernanceStage : AdmissionStage, CompetingRequests);
            }
            return await completion;
        }
        catch
        {
            primary.CancelPendingRequests();
            secondary.CancelPendingRequests();
            await DrainAsync(completion);
            throw;
        }
    }

    /// <summary>Drains both HTTP owners after observation failure without retaining secret response diagnostics.</summary>
    private static async Task DrainAsync(Task<HttpResponseMessage[]>? completion)
    {
        if (completion is null)
        {
            return;
        }
        _ = await Record.ExceptionAsync(async () =>
        {
            var responses = await completion.WaitAsync(TimeSpan.FromSeconds(DrainSeconds));
            foreach (var response in responses)
            {
                response.Dispose();
            }
        });
    }
}
