using System.Text.Json;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Governance.Driver.Features.Protocol;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;

namespace IdentityPlatform.Governance.Driver.Features.Operations;

/// <summary>Dispatches only serialized observation or one native governed command; no arbitrary manager or SQL operation is accepted.</summary>
internal sealed class DriverOperation(GovernanceService governance)
{
    private const string Phase = "result";

    /// <summary>Calls the actual internal service with the unchanged outer operation ceiling and untrusted session candidate.</summary>
    internal async Task<DriverResult> RunAsync(DriverInput input, CancellationToken token)
    {
        if (input.Operation == DriverLimits.Read)
        {
            var observation = await governance.ReadAsync(input.ActorId!, token);
            return new DriverResult(false, JsonSerializer.Serialize(new
            {
                phase = Phase,
                operation = DriverLimits.Read,
                authorityRevision = observation.AuthorityRevision,
                permissionCount = observation.Permissions.Count,
                usableAdministratorCount = observation.UsableAdministrators.Count,
                actorIsUsable = observation.UsableAdministrators.Contains(input.ActorId!)
            }) + DriverLimits.Newline);
        }
        var outcome = await governance.ExecuteAsync(input.Command!.Create(), input.Candidate!.Create(),
            input.Password!, input.Code!, token);
        var result = outcome switch
        {
            GovernanceOutcome.Accepted => DriverLimits.Accepted,
            GovernanceOutcome.Denied => DriverLimits.Denied,
            GovernanceOutcome.NoChange => DriverLimits.NoChange,
            _ => throw new InvalidOperationException()
        };
        return new DriverResult(outcome == GovernanceOutcome.Accepted, JsonSerializer.Serialize(new
        {
            phase = Phase,
            operation = DriverLimits.Execute,
            outcome = result
        }) + DriverLimits.Newline);
    }
}

/// <summary>Contains only fixed outcome labels and nonsecret aggregate observations, never command or credential input.</summary>
internal sealed record DriverResult(bool AcceptedCommit, string Frame);
