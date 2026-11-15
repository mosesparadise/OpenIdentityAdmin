using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Freezes nonsecret approval provenance supplied by the already-authorized ordinary or retirement admission owner.</summary>
internal sealed record GrantApprovalContent(string ActorId, string ApplicationId, GrantApprovalAction Action,
    ApplicationLifecycleReason Reason, Guid RequestKey, string ScopedKeyHash, string RequestHash);

/// <summary>Returns new durable progress and its frozen persistence verifier to the original transaction owner.</summary>
internal sealed record GrantAdmissionState(ApplicationGrantJob Job, Guid ApprovalId, GrantPersistence Persistence);
