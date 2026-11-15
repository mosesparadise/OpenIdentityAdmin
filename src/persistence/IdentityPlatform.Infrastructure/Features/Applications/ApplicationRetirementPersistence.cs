using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes newly admitted retirement and audit before save callbacks can alter either local or persisted state.</summary>
internal sealed class ApplicationRetirementPersistence
{
    private readonly ApplicationRetirement receipt;
    private readonly SecurityAuditIntent audit;
    private readonly string expectedReceipt;
    private readonly string expectedAudit;
    private readonly string applicationId;
    private readonly Guid auditId;

    /// <summary>Captures exact approved nonsecret scalars independently of mutable EF entities and response projection.</summary>
    internal ApplicationRetirementPersistence(ApplicationRetirement receipt, SecurityAuditIntent audit)
    {
        this.receipt = receipt;
        this.audit = audit;
        applicationId = receipt.ApplicationId;
        auditId = audit.Id;
        expectedReceipt = JsonSerializer.Serialize(receipt);
        expectedAudit = JsonSerializer.Serialize(audit);
    }

    /// <summary>Rejects deleted, suppressed, substituted or locally changed terminal intent before the original transaction commits.</summary>
    internal ValueTask VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => scope.Owner.RunAsync(async current =>
    {
        var savedReceipt = await scope.Owner.Database.Set<ApplicationRetirement>().AsNoTracking().SingleOrDefaultAsync(row => row.ApplicationId == applicationId, current);
        var savedAudit = await scope.Owner.Database.Set<SecurityAuditIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == auditId, current);
        if (JsonSerializer.Serialize(receipt) != expectedReceipt || JsonSerializer.Serialize(audit) != expectedAudit ||
            JsonSerializer.Serialize(savedReceipt) != expectedReceipt || JsonSerializer.Serialize(savedAudit) != expectedAudit)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }, token);
}
