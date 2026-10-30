namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Carries protected browser intent only; each operation independently requires current native, managed and account state.</summary>
internal sealed class ManagedHumanRequest
{
    public string Selector { get; init; } = string.Empty;
    public string BrowserDigest { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string Callback { get; init; } = string.Empty;
    public string DemandDigest { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public string RequestUri { get; init; } = string.Empty;
    public string Correlation { get; init; } = string.Empty;
    public long ExpiresAt
    {
        get; init;
    }
    public long Revision
    {
        get; init;
    }
    public ManagedLoginState State
    {
        get; init;
    }
}

/// <summary>Names the only supported managed human actions; no code/token or administrator-grant action exists here.</summary>
internal enum ManagedHumanAction
{
    Read, Password, Mfa, Approve, Deny
}

/// <summary>Returns finite presentation and transition data only after the owning coordinator has committed and released the transaction.</summary>
internal sealed record ManagedHumanResult(bool Accepted, ManagedLoginState State, long Revision, string ClientId, long ExpiresAt, bool NeedsDummyWork = false);
