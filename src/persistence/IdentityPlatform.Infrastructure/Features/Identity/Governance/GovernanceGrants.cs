using System.Collections.Frozen;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Core.Features.Identity.Permissions;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Reads bounded persisted grants under the original locks and never fills removed values from the catalogue.</summary>
internal sealed class GovernanceGrants(PlatformDbContext database)
{
    /// <summary>Loads the bounded role catalogue before ordinal filtering so provider collation cannot conceal aliases.</summary>
    internal async Task<IReadOnlyList<IdentityRole>> ReadRolesAsync(CancellationToken token)
    {
        var roles = await database.Roles.AsNoTracking().OrderBy(row => row.Id).Take(GovernanceLimits.Roles + 1).ToListAsync(token);
        if (roles.Count > GovernanceLimits.Roles || roles.Any(role => !GovernanceRequest.IsIdentifier(role.Id)))
        {
            throw new FactorUnavailableException();
        }
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            var matches = AdministratorPermissions.Bundles.Keys.Where(value =>
                string.Equals(value, role.Name?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, role.NormalizedName?.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1)
            {
                throw new FactorUnavailableException();
            }
            var name = matches.SingleOrDefault();
            var normalized = name?.ToUpperInvariant();
            if (name is not null && (role.Name != name || role.NormalizedName != normalized || !reserved.Add(name)))
            {
                throw new FactorUnavailableException();
            }
        }
        return roles.AsReadOnly();
    }

    /// <summary>Builds an immutable, transaction-local observation for every declared account and explicit role target.</summary>
    internal async Task<GovernanceGrantView> ReadAsync(CeremonyTransaction transaction, string? targetRole)
    {
        transaction.RequireContext(database);
        transaction.RequireGovernance();
        return await ReadCoreAsync(transaction.Plan.Users, targetRole, transaction.Token);
    }

    /// <summary>Reuses bounded persisted grant semantics for exactly one account under the live protocol issuance locks.</summary>
    internal async Task<GovernanceGrantView> ReadProtocolAsync(ProtocolAdmissionOwnership ownership, CancellationToken token)
    {
        ownership.RequireExecution(database, token);
        var result = await ReadCoreAsync([ownership.UserId], null, token);
        ownership.RequireExecution(database, token);
        return result;
    }

    /// <summary>Reads only the declared accounts and relevant persisted role claims after the caller has proved its lock ownership.</summary>
    private async Task<GovernanceGrantView> ReadCoreAsync(IReadOnlyList<string> users, string? targetRole, CancellationToken token)
    {
        var roles = (await ReadRolesAsync(token)).ToDictionary(role => role.Id, StringComparer.Ordinal);
        var memberships = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal);
        foreach (var userId in users)
        {
            memberships.Add(userId, await ReadMembershipsAsync(userId, roles, token));
        }
        var relevant = memberships.Values.SelectMany(value => value).ToHashSet(StringComparer.Ordinal);
        if (targetRole is not null)
        {
            relevant.Add(targetRole);
        }
        var grants = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal);
        foreach (var roleId in relevant)
        {
            if (!roles.TryGetValue(roleId, out var role))
            {
                throw new FactorUnavailableException();
            }
            grants.Add(roleId, await ReadClaimsAsync(role, token));
        }
        return new GovernanceGrantView(roles.Values, memberships, grants);
    }

    /// <summary>Rejects overflow, duplicate native memberships and identifiers that only match under database collation.</summary>
    private async Task<FrozenSet<string>> ReadMembershipsAsync(string userId, Dictionary<string, IdentityRole> roles, CancellationToken token)
    {
        var rows = await database.UserRoles.AsNoTracking().Where(row => row.UserId == userId)
            .OrderBy(row => row.RoleId).Take(GovernanceLimits.Memberships + 1).ToListAsync(token);
        var identifiers = rows.Select(row => row.RoleId).ToFrozenSet(StringComparer.Ordinal);
        if (rows.Count > GovernanceLimits.Memberships || identifiers.Count != rows.Count ||
            rows.Any(row => row.UserId != userId || !roles.ContainsKey(row.RoleId)))
        {
            throw new FactorUnavailableException();
        }
        return identifiers;
    }

    /// <summary>Accepts only exact, unique, finite permission claims on the corresponding reserved bundle.</summary>
    private async Task<FrozenSet<string>> ReadClaimsAsync(IdentityRole role, CancellationToken token)
    {
        var rows = await database.RoleClaims.AsNoTracking().Where(row => row.RoleId == role.Id)
            .OrderBy(row => row.Id).Take(GovernanceLimits.Claims + 1).ToListAsync(token);
        if (rows.Count > GovernanceLimits.Claims || rows.Any(row => row.RoleId != role.Id))
        {
            throw new FactorUnavailableException();
        }
        var grants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!string.Equals(row.ClaimType?.Trim(), AdministratorPermissions.ClaimType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (row.ClaimType != AdministratorPermissions.ClaimType || role.Name is null ||
                !AdministratorPermissions.Bundles.TryGetValue(role.Name, out var allowed) ||
                row.ClaimValue is null || !allowed.Contains(row.ClaimValue) || !grants.Add(row.ClaimValue))
            {
                throw new FactorUnavailableException();
            }
        }
        return grants.ToFrozenSet(StringComparer.Ordinal);
    }
}

/// <summary>Exposes immutable observed grants; possessing this value never authorizes a mutation.</summary>
internal sealed class GovernanceGrantView
{
    private readonly FrozenDictionary<string, FrozenSet<string>> memberships;
    private readonly FrozenDictionary<string, FrozenSet<string>> grants;
    private readonly FrozenDictionary<string, string?> names;
    internal FrozenDictionary<string, string?> Stamps
    {
        get;
    }

    /// <summary>Copies native observations so later tracking or caller collection changes cannot alter the decision.</summary>
    internal GovernanceGrantView(IEnumerable<IdentityRole> roles, Dictionary<string, FrozenSet<string>> memberships,
        Dictionary<string, FrozenSet<string>> grants)
    {
        this.memberships = memberships.ToFrozenDictionary(StringComparer.Ordinal);
        this.grants = grants.ToFrozenDictionary(StringComparer.Ordinal);
        names = roles.ToFrozenDictionary(role => role.Id, role => role.Name, StringComparer.Ordinal);
        Stamps = roles.ToFrozenDictionary(role => role.Id, role => role.ConcurrencyStamp, StringComparer.Ordinal);
    }

    /// <summary>Unions actual stored values only across this account's recognized role memberships.</summary>
    internal FrozenSet<string> Permissions(string userId) => memberships[userId].SelectMany(role => grants[role]).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Checks exact native membership without assigning meaning to user or token claims.</summary>
    internal bool HasMembership(string userId, string roleId) => memberships[userId].Contains(roleId);

    /// <summary>Returns the validated reserved bundle name or rejects an unsupported custom-role command.</summary>
    internal string ReservedName(string roleId) => names.TryGetValue(roleId, out var name) &&
        name is not null && AdministratorPermissions.Bundles.ContainsKey(name) ? name : throw new FactorUnavailableException();

    /// <summary>Checks a persisted grant independently of the bundle's bootstrap defaults.</summary>
    internal bool HasGrant(string roleId, string permission) => grants[roleId].Contains(permission);

    /// <summary>Requires the full platform bundle on its own exact role, not a union of specialist memberships.</summary>
    internal bool HasPlatformBundle(string userId) => memberships[userId].Any(roleId =>
        names[roleId] == AdministratorPermissions.PlatformRole &&
        grants[roleId].SetEquals(AdministratorPermissions.Bundles[AdministratorPermissions.PlatformRole]));

    /// <summary>Copies a validated observation into a projected decision without editing native entities or granting authority.</summary>
    private GovernanceGrantView(GovernanceGrantView original, Dictionary<string, FrozenSet<string>> memberships,
        Dictionary<string, FrozenSet<string>> grants)
    {
        this.memberships = memberships.ToFrozenDictionary(StringComparer.Ordinal);
        this.grants = grants.ToFrozenDictionary(StringComparer.Ordinal);
        names = original.names;
        Stamps = original.Stamps;
    }

    /// <summary>Applies only the command's finite membership/grant transition for pre-write survivor evaluation.</summary>
    internal GovernanceGrantView Project(GovernanceRequest request)
    {
        var projectedMemberships = memberships.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var projectedGrants = grants.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (request.Action is GovernanceAction.AssignBundle or GovernanceAction.RemoveBundle)
        {
            var roles = memberships[request.UserId!].ToHashSet(StringComparer.Ordinal);
            if (request.Action == GovernanceAction.AssignBundle)
            {
                roles.Add(request.RoleId!);
            }
            else
            {
                roles.Remove(request.RoleId!);
            }
            projectedMemberships[request.UserId!] = roles.ToFrozenSet(StringComparer.Ordinal);
        }
        if (request.Action is GovernanceAction.AddGrant or GovernanceAction.RemoveGrant)
        {
            var values = grants[request.RoleId!].ToHashSet(StringComparer.Ordinal);
            if (request.Action == GovernanceAction.AddGrant)
            {
                values.Add(request.Permission!);
            }
            else
            {
                values.Remove(request.Permission!);
            }
            projectedGrants[request.RoleId!] = values.ToFrozenSet(StringComparer.Ordinal);
        }
        return new GovernanceGrantView(this, projectedMemberships, projectedGrants);
    }
}
