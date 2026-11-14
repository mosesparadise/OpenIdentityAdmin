using System.Text.Json;
using IdentityPlatform.Governance.Driver.Features.Operations;
using IdentityPlatform.Governance.Driver.Features.Protocol;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Governance.Driver.Features.ManagedPolicy;

/// <summary>Retains an immutable policy in a real second process and verifies current policy only after the parent releases an observed database lock.</summary>
internal static class ManagedPolicyOperation
{
    private const string ResultPhase = "result";
    private const string OpenId = "openid";

    /// <summary>Warms the real production manager path before readiness, with no entity-cache claim because production disables entity caching.</summary>
    internal static async Task<DriverResult> RunAsync(ServiceProvider services, DriverInput input, Stream pipe, CancellationToken token)
    {
        var original = await ReadAsync(services, input, null, token);
        await DriverProtocol.ReadyAsync(token);
        await DriverProtocol.AcknowledgeAsync(pipe, token);
        var current = await ReadAsync(services, input, original.Decision, token);
        return new(false, JsonSerializer.Serialize(new
        {
            phase = ResultPhase,
            operation = DriverLimits.ManagedPolicy,
            originalRejected = !current.Matches,
            currentChanged = ManagedScopeCodec.Encode(original.Decision) != ManagedScopeCodec.Encode(current.Decision),
            revisionUnchanged = original.Decision.ApplicationRevision == current.Decision.ApplicationRevision &&
                original.Decision.Scopes.Select(value => value.Revision).SequenceEqual(current.Decision.Scopes.Select(value => value.Revision))
        }) + DriverLimits.Newline);
    }

    /// <summary>Uses a fresh original owner per read, the real governance lock and actual rollback/cleanup before returning data-only evidence.</summary>
    private static async Task<Observation> ReadAsync(IServiceProvider services, DriverInput input, ManagedScopeDecision? expected, CancellationToken token)
    {
        var request = new ProtocolStorageRequest(TimeProvider.System, token);
        try
        {
            return await request.RunAsync(services.GetRequiredService<IServiceScopeFactory>(), async (scope, current) =>
            {
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                var names = new[] { OpenId, input.ScopeName! }.Order(StringComparer.Ordinal).ToArray();
                var matches = expected is null || await ManagedScopePolicy.TryVerifyAsync(scope, input.ClientId!, expected, current);
                var decision = await ManagedScopePolicy.CaptureAsync(scope, input.ClientId!, names, names, current);
                if (expected is null)
                {
                    await ManagedScopePolicy.VerifyAsync(scope, input.ClientId!, decision, current);
                }
                return new Observation(decision, matches);
            });
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Retains bounded metadata only within the child; only finite boolean evidence is written to its output pipe.</summary>
    private sealed record Observation(ManagedScopeDecision Decision, bool Matches);
}
