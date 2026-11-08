using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Core.Tests.Features.Catalogue;

/// <summary>Checks reserved identities, hostile grammar and claim/resource policy confusion before persistence or protocol use.</summary>
public sealed class CataloguePolicyTests
{
    private const int IdentifierLimit = 100;
    private const int IdentifierOverflow = 101;
    private const int TextLimit = 200;
    private const int TextOverflow = 201;
    private const int ConsentLimit = 500;
    private const int ConsentOverflow = 501;
    private const int AssignmentLimit = 32;
    private const int AssignmentOverflow = 33;

    private const string ReadScope = "orders.read";
    private const string CaseReadScope = "Orders.Read";
    private const string IdentityKind = "identity";
    private const string ApiKind = "api";
    private const string NoDisclosure = "none";
    private const string EmailPolicy = "email";
    private const string EmailAlias = "Email";
    private const string DisplayName = "Example";
    private const string Consent = "Consent";
    private const string EmailConsent = "Share email";
    private const string CustomScope = "custom-scope";
    private const string ScopePrefix = "scope";
    private const string AudienceId = "audience";
    private const string ResourceName = "orders";
    private const string InvalidOwner = "owner\n";
    private const string PrefixResource = "identityplatform-admin-api";
    private const string AdminResource = "identity-admin-api";
    private const string AdminResourceAlias = "IDENTITY-ADMIN-API";
    private const string AdminScope = "identity.admin";
    private const string AdminScopeAlias = "IDENTITY.ADMIN";
    private const string PilotScope = "pilot_api";
    private const string ResourceUri = "https://api.example/orders";
    private const string QueryResource = "https://api.example/orders?aud=admin";
    private const string SpacedId = "native id";
    private const string ControlId = "native\0id";
    private const string MalformedId = "native\ud800";
    private const string OpenId = "openid";
    private const string OfflineAccess = "offline_access";
    private const string ConsentField = "consentText";

    /// <summary>Protocol identifiers preserve case while rejecting special/system aliases and ambiguous parser input.</summary>
    [Theory]
    [InlineData(ReadScope, true)]
    [InlineData("Orders:Read-v2_1", true)]
    [InlineData(EmailPolicy, true)]
    [InlineData(OpenId, false)]
    [InlineData("OPENID", false)]
    [InlineData(OfflineAccess, false)]
    [InlineData("profile", false)]
    [InlineData("phone", false)]
    [InlineData("address", false)]
    [InlineData(AdminScope, false)]
    [InlineData(AdminScopeAlias, false)]
    [InlineData(AdminResource, false)]
    [InlineData(PilotScope, false)]
    [InlineData("PILOT_API", false)]
    [InlineData("IdentityPlatform-admin", false)]
    [InlineData(" orders.read", false)]
    [InlineData("orders.read ", false)]
    [InlineData("orders\tread", false)]
    [InlineData("orders/read", false)]
    [InlineData("orders\"read", false)]
    [InlineData("orders\\read", false)]
    [InlineData("ordérs", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Given_ProtocolName_When_Validated_Then_OnlyExactNonreservedGrammarPasses(string? value, bool valid) =>
        Assert.Equal(valid, CatalogueRules.IsName(value));

    /// <summary>Enforces independently stated native and wire bounds, including exact equality and one character beyond.</summary>
    [Fact]
    public void Given_IdentifierBounds_When_EqualityAndOverflowChecked_Then_OnlyBoundedValuesPass()
    {
        Assert.True(CatalogueRules.IsName(new string('a', IdentifierLimit)));
        Assert.False(CatalogueRules.IsName(new string('a', IdentifierOverflow)));
        Assert.True(CatalogueRules.IsResource(new string('a', TextLimit)));
        Assert.False(CatalogueRules.IsResource(new string('a', TextOverflow)));
        Assert.True(CatalogueRules.IsResource(ResourceUri));
        Assert.False(CatalogueRules.IsResource(QueryResource));
        Assert.False(CatalogueRules.IsResource(PrefixResource));
        Assert.False(CatalogueRules.IsResource(AdminResource));
        Assert.False(CatalogueRules.IsResource(AdminResourceAlias));
        Assert.False(CatalogueRules.IsResource(AdminScope));
        Assert.False(CatalogueRules.IsResource(PilotScope));
        Assert.True(CatalogueRules.IsIdentifier(new string('x', IdentifierLimit)));
        Assert.False(CatalogueRules.IsIdentifier(new string('x', IdentifierOverflow)));
        Assert.False(CatalogueRules.IsIdentifier(SpacedId));
        Assert.False(CatalogueRules.IsIdentifier(ControlId));
        Assert.False(CatalogueRules.IsIdentifier(MalformedId));
    }

    /// <summary>Only identity scopes may disclose email, and every API scope requires exactly one opaque audience assignment.</summary>
    [Theory]
    [InlineData(IdentityKind, null, NoDisclosure, true)]
    [InlineData(IdentityKind, null, EmailPolicy, true)]
    [InlineData(IdentityKind, "audience-id", NoDisclosure, false)]
    [InlineData(ApiKind, "audience-id", NoDisclosure, true)]
    [InlineData(ApiKind, "audience-id", EmailPolicy, false)]
    [InlineData(ApiKind, null, NoDisclosure, false)]
    [InlineData(ApiKind, "", NoDisclosure, false)]
    [InlineData(ApiKind, "audience id", NoDisclosure, false)]
    [InlineData("Identity", null, NoDisclosure, false)]
    [InlineData("unknown", null, NoDisclosure, false)]
    [InlineData(IdentityKind, null, "roles", false)]
    [InlineData(IdentityKind, null, EmailAlias, false)]
    public void Given_ClaimAndResourcePolicy_When_Validated_Then_OnlySupportedCombinationPasses(string kind, string? audience, string disclosure, bool valid) =>
        Assert.Equal(valid, CatalogueRules.Validate(new ScopeInput(CustomScope, DisplayName, kind, true, Consent, audience, disclosure)).Count == 0);

    /// <summary>The standard email scope cannot be repurposed as API authority or a misleading no-disclosure alias.</summary>
    [Fact]
    public void Given_EmailScope_When_KindOrDisclosureConflicts_Then_StandardMeaningCannotBeOverridden()
    {
        Assert.Empty(CatalogueRules.Validate(new ScopeInput(EmailPolicy, EmailAlias, IdentityKind, true, EmailConsent, null, EmailPolicy)));
        Assert.NotEmpty(CatalogueRules.Validate(new ScopeInput(EmailAlias, EmailAlias, IdentityKind, true, EmailConsent, null, EmailPolicy)));
        Assert.NotEmpty(CatalogueRules.Validate(new ScopeInput(EmailPolicy, EmailAlias, IdentityKind, true, EmailConsent, null, NoDisclosure)));
        Assert.NotEmpty(CatalogueRules.Validate(new ScopeInput(EmailPolicy, EmailAlias, ApiKind, true, EmailConsent, AudienceId, NoDisclosure)));
    }

    /// <summary>Assignment supports deliberate removal but never normalizes duplicates or silently drops excessive/reserved scopes.</summary>
    [Fact]
    public void Given_Assignment_When_EmptyDuplicateReservedOrOversized_Then_IntentIsNotSilentlyChanged()
    {
        Assert.True(CatalogueRules.IsAssignment([]));
        Assert.True(CatalogueRules.IsAssignment([ReadScope, CaseReadScope]));
        Assert.False(CatalogueRules.IsAssignment([ReadScope, ReadScope]));
        Assert.False(CatalogueRules.IsAssignment([OpenId]));
        Assert.False(CatalogueRules.IsAssignment([OfflineAccess]));
        Assert.False(CatalogueRules.IsAssignment([AdminScope]));
        Assert.False(CatalogueRules.IsAssignment([AdminScopeAlias]));
        Assert.False(CatalogueRules.IsAssignment([PilotScope]));
        Assert.False(CatalogueRules.IsAssignment(null));
        Assert.False(CatalogueRules.IsAssignment([null!]));
        Assert.True(CatalogueRules.IsAssignment(Enumerable.Range(0, AssignmentLimit).Select(value => ScopePrefix + value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
        Assert.False(CatalogueRules.IsAssignment(Enumerable.Range(0, AssignmentOverflow).Select(value => ScopePrefix + value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
    }

    /// <summary>Presentation errors remain finite and value-free even for oversized or malformed current inputs.</summary>
    [Fact]
    public void Given_PresentationBounds_When_InvalidInputRejected_Then_ErrorDoesNotEchoValues()
    {
        Assert.Empty(CatalogueRules.Validate(new AudienceInput(ResourceName, new string('x', TextLimit), new string('y', TextLimit), false)));
        var audience = CatalogueRules.Validate(new AudienceInput(ResourceName, new string('x', TextOverflow), InvalidOwner, true));
        Assert.Equal(["displayName", "owner"], audience.Keys.Order(StringComparer.Ordinal));
        Assert.All(audience.Values.SelectMany(value => value), value => Assert.Equal("The value is missing, unsupported or outside its allowed bounds.", value));
        Assert.Empty(CatalogueRules.Validate(new ScopeInput(ScopePrefix, DisplayName, IdentityKind, false, new string('x', ConsentLimit), null, NoDisclosure)));
        Assert.Contains(ConsentField, CatalogueRules.Validate(new ScopeInput(ScopePrefix, DisplayName, IdentityKind, true, new string('x', ConsentOverflow), null, NoDisclosure)).Keys);
    }
}
