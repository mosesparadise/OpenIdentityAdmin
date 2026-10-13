using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Validates the actual native outcome independently of manager success flags or caller assertions.</summary>
internal static class GovernanceEffect
{
    /// <summary>Requires the fixed command's requested membership, permission or account state to exist after persistence.</summary>
    internal static void Require(GovernanceState state, GovernanceGrantView current)
    {
        var request = state.Request;
        var applied = request.Action switch
        {
            GovernanceAction.AssignBundle => current.HasMembership(request.UserId!, request.RoleId!),
            GovernanceAction.RemoveBundle => !current.HasMembership(request.UserId!, request.RoleId!),
            GovernanceAction.AddGrant => current.HasGrant(request.RoleId!, request.Permission!),
            GovernanceAction.RemoveGrant => !current.HasGrant(request.RoleId!, request.Permission!),
            GovernanceAction.EnableAccount => state.Target!.User.IsEnabled,
            GovernanceAction.DisableAccount => !state.Target!.User.IsEnabled,
            GovernanceAction.SuspendAccount => state.Target!.Enrollment.Suspended,
            GovernanceAction.ResumeAccount => !state.Target!.Enrollment.Suspended,
            _ => false
        };
        if (!applied)
        {
            throw new FactorUnavailableException();
        }
    }
}
