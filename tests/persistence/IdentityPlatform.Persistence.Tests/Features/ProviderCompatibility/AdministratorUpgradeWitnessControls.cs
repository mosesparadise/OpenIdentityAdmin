using System.Text.Json;
using Xunit.Sdk;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Seeds forged catalog provenance and damaged old metadata so the additive-upgrade oracle cannot pass on names alone.</summary>
internal static class AdministratorUpgradeWitnessControls
{
    private static readonly string[] OldValues = ["platform", "AspNetUsers", "old-check", "original-definition"];
    private static readonly string[] NewValues = ["platform", "AdministratorProtocolProfile", "new-check", "new-definition"];
    private static readonly string?[][] RejectedValues =
    [
        ["platform", "AspNetUsers", "CK_AdministratorProtocolProfile_State", "forged-check"],
        ["platform", "AspNetUsers", "IX_AdministratorProtocolProfile_extra", "forged-index"],
        ["bff", "AdministratorProtocolProfile", "new-check", "wrong-schema"],
        [null, null, "CK_AdministratorProtocolProfile_State", "unknown-or-ambiguous-owner"]
    ];

    /// <summary>Requires one correct additive control and rejects forged owners, changed old checks and lost duplicate metadata.</summary>
    internal static void Verify()
    {
        var oldRow = JsonSerializer.Serialize(OldValues);
        var newRow = JsonSerializer.Serialize(NewValues);
        string[] before = [oldRow, oldRow];
        AdministratorUpgradeWitness.RequireCatalog(before, [oldRow, oldRow, newRow]);
        foreach (var rejected in RejectedValues)
        {
            Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before, [oldRow, oldRow, JsonSerializer.Serialize(rejected)]));
        }
        Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before, [oldRow, newRow]));
        Assert.ThrowsAny<XunitException>(() => AdministratorUpgradeWitness.RequireCatalog(before, [newRow]));
    }
}
