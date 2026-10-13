namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Declares P12's independent boundary values without importing production capacity constants.</summary>
internal sealed record CapacityCase(string Name, CapacityKind Kind, int Limit, int Count)
{
    internal bool Overflows => Count > Limit;
}

/// <summary>Identifies the four independently saturated authority collections.</summary>
internal enum CapacityKind
{
    Accounts, Memberships, Roles, Claims
}

/// <summary>Keeps the eight predeclared P12 identities and independent limits together.</summary>
internal static class CapacityCases
{
    private const int Accounts = 256;
    private const int Memberships = 64;
    private const int Roles = 128;
    private const int Claims = 128;
    internal static readonly CapacityCase[] All =
    [
        new("accounts-256", CapacityKind.Accounts, Accounts, Accounts),
        new("accounts-257", CapacityKind.Accounts, Accounts, Accounts + 1),
        new("memberships-64", CapacityKind.Memberships, Memberships, Memberships),
        new("memberships-65", CapacityKind.Memberships, Memberships, Memberships + 1),
        new("roles-128", CapacityKind.Roles, Roles, Roles),
        new("roles-129", CapacityKind.Roles, Roles, Roles + 1),
        new("claims-128", CapacityKind.Claims, Claims, Claims),
        new("claims-129", CapacityKind.Claims, Claims, Claims + 1)
    ];
}
