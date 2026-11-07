using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Core.Tests.Features.ApplicationGrants;

/// <summary>Exercises hostile wire aliases and action confusion before any destructive request can be accepted.</summary>
public sealed class GrantRevocationTests
{
    private const string SecurityReason = "security";
    private const string Wildcard = "*";
    private const string WeakPrefix = "W/";

    private const string Key = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";

    /// <summary>Requires one exact nonempty key representation rather than accepting Guid parser aliases.</summary>
    [Theory]
    [InlineData(Key, true)]
    [InlineData("AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA", false)]
    [InlineData("aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa", false)]
    [InlineData("{aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa}", false)]
    [InlineData(" aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData(null, false)]
    public void Given_RequestKeyAliases_When_Validated_Then_OnlyCanonicalNonemptyKeyPasses(string? value, bool valid) =>
        Assert.Equal(valid, GrantRevocationRules.TryKey(value, out _));

    /// <summary>Rejects implicit confirmation, unknown reasons and accidental continuation fields on submission.</summary>
    [Theory]
    [InlineData(SecurityReason, true, false, true)]
    [InlineData("maintenance", true, false, true)]
    [InlineData("configuration", true, false, true)]
    [InlineData("Security", true, false, false)]
    [InlineData("security ", true, false, false)]
    [InlineData("arbitrary", true, false, false)]
    [InlineData(SecurityReason, false, false, false)]
    [InlineData(SecurityReason, true, true, false)]
    public void Given_DestructiveInput_When_SubmissionValidated_Then_OnlyExplicitFiniteIntentPasses(string reason, bool confirm, bool jobVersion, bool valid) =>
        Assert.Equal(valid, GrantRevocationRules.Reason(new(reason, confirm, jobVersion ? ApplicationVersion.Create(Key, 1) : null), false) is not null);

    /// <summary>Requires an exact observed job version for recovery; wildcard or absent versions cannot resume work.</summary>
    [Fact]
    public void Given_Continuation_When_JobPreconditionMissingOrAliased_Then_OnlyCanonicalVersionPasses()
    {
        Assert.Null(GrantRevocationRules.Reason(new(SecurityReason, true), true));
        Assert.Null(GrantRevocationRules.Reason(new(SecurityReason, true, Wildcard), true));
        Assert.Null(GrantRevocationRules.Reason(new(SecurityReason, true, WeakPrefix + ApplicationVersion.Create(Key, 1)), true));
        Assert.Equal(ApplicationLifecycleReason.Security,
            GrantRevocationRules.Reason(new(SecurityReason, true, ApplicationVersion.Create(Key, 1)), true));
    }
}
