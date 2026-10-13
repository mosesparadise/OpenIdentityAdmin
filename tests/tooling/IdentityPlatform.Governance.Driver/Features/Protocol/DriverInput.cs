using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.Governance.Driver.Features.Protocol;

/// <summary>Contains one bounded synthetic request; this class deliberately has no value-printing record implementation.</summary>
internal sealed class DriverInput
{
    public required string Operation
    {
        get; init;
    }
    public required string Provider
    {
        get; init;
    }
    public required string RuntimeConnection
    {
        get; init;
    }
    public required Dictionary<string, string?> IssuerSettings
    {
        get; init;
    }
    public string? ActorId
    {
        get; init;
    }
    public DriverCommand? Command
    {
        get; init;
    }
    public DriverCandidate? Candidate
    {
        get; init;
    }
    public string? Password
    {
        get; init;
    }
    public string? Code
    {
        get; init;
    }
    public bool AwaitAcknowledgement
    {
        get; init;
    }

    /// <summary>Rejects irrelevant fields and oversize values before configuring native services.</summary>
    internal void RequireShape()
    {
        RequireBound(RuntimeConnection, DriverLimits.SecretCharacters);
        if (Provider is not (DriverLimits.PostgreSql or DriverLimits.SqlServer) || IssuerSettings is null)
        {
            throw new InvalidOperationException();
        }
        DriverIssuerSettings.RequireAllowed(IssuerSettings);
        if (Operation == DriverLimits.Read)
        {
            RequireBound(ActorId, DriverLimits.IdentifierCharacters);
            if (Command is not null || Candidate is not null || Password is not null || Code is not null || AwaitAcknowledgement)
            {
                throw new InvalidOperationException();
            }
            return;
        }
        if (Operation != DriverLimits.Execute || ActorId is not null || Command is null || Candidate is null)
        {
            throw new InvalidOperationException();
        }
        RequireBound(Candidate.UserId, DriverLimits.IdentifierCharacters);
        RequireBound(Candidate.SessionId, DriverLimits.IdentifierCharacters);
        RequireBound(Password, DriverLimits.SecretCharacters);
        RequireBound(Code, DriverLimits.CodeCharacters);
        _ = Command.Create();
    }

    /// <summary>Bounds values without normalizing credentials or granting identifier validity.</summary>
    private static void RequireBound(string? value, int length)
    {
        if (value is null || value.Length > length)
        {
            throw new InvalidOperationException();
        }
    }
}

/// <summary>Carries only untrusted cookie identifiers, never a serialized production proof.</summary>
internal sealed class DriverCandidate
{
    public required string UserId
    {
        get; init;
    }
    public required string SessionId
    {
        get; init;
    }

    /// <summary>Constructs the untrusted candidate consumed by real stored-session verification.</summary>
    internal JourneySessionCandidate Create() => new(UserId, SessionId);
}

/// <summary>Copies finite caller intent through the production request factory.</summary>
internal sealed class DriverCommand
{
    public required string Action
    {
        get; init;
    }
    public required GovernanceVersions Versions
    {
        get; init;
    }
    public string? UserId
    {
        get; init;
    }
    public string? RoleId
    {
        get; init;
    }
    public string? Permission
    {
        get; init;
    }

    /// <summary>Rejects numeric, case-folded, unsupported and malformed command shapes.</summary>
    internal GovernanceRequest Create()
    {
        if (!Enum.TryParse<GovernanceAction>(Action, out var action) || !Enum.IsDefined(action) || action.ToString() != Action)
        {
            throw new InvalidOperationException();
        }
        return GovernanceRequest.Create(action, Versions, UserId, RoleId, Permission) ?? throw new InvalidOperationException();
    }
}
