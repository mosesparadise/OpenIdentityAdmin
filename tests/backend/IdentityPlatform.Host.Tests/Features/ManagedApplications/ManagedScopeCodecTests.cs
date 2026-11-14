using System.Collections.Immutable;
using System.Text;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Exercises canonical durable decisions independently of current protocol activation, including hostile persisted representations.</summary>
public sealed class ManagedScopeCodecTests
{
    private const string Application = "synthetic-managed-application";
    private const string OpenId = "openid";
    private const string ScopeName = "orders.read";
    private const int MaximumBytes = 16384;
    private static readonly string Binding = new('A', 64);

    private const string Whitespace = " ";
    private const string DuplicateDefect = "duplicate";
    private const string UnknownDefect = "unknown";
    private const string WhitespaceDefect = "whitespace";
    private const string VersionDefect = "version";
    private const string MissingDefect = "missing";
    private const string NullDefect = "null";
    private const string DigestDefect = "digest";
    private const string OriginalVersion = "\"Version\":1";
    private const string DuplicateVersion = "\"Version\":1,\"Version\":1";
    private const string ExtraMember = "\"Extra\":true,";
    private const string FutureVersion = "\"Version\":2";
    private const string ScopesMember = "\"Scopes\":[],";
    private const string EmptyScopes = "\"Scopes\":[]";
    private const string NullScopes = "\"Scopes\":null";
    private const int MaximumScopes = 32;
    private const int OverflowScopes = 33;
    private const string AudienceId = "audience";
    private const string AudienceResource = "orders";
    private const int MultibyteIdLength = 50;
    private const int MaximumAsciiPadding = 45;
    private const string AsciiPadding = "x";
    private const string ScopeIdPrefix = "scope-";
    private const string ScopeNamePrefix = "scope.";
    private const string ScopeNumberFormat = "D2";
    private const string IdentityKind = "identity";
    private const string NoDisclosure = "none";

    /// <summary>Historical rows are accepted only with both columns absent; a new row must match the exact application, revision and canonical bytes.</summary>
    [Fact]
    public void Given_LegacyAndNewInteractions_When_BindingsAreChanged_Then_OnlyExactOpenIdRemainsEligible()
    {
        var row = new ManagedLoginInteraction { ApplicationId = Application, ApplicationRevision = 1 };
        Assert.True(ManagedScopeBinding.IsOpenId(row));
        row.PolicyDigest = Binding;
        Assert.False(ManagedScopeBinding.IsOpenId(row));
        row.PolicyDigest = null;
        ManagedScopeBinding.Attach(row, ManagedScopeBinding.OpenId(Application, 1));
        Assert.True(ManagedScopeBinding.IsOpenId(row));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeBinding.Attach(row, ManagedScopeBinding.OpenId(Application, 1)));
        row.ApplicationRevision++;
        Assert.False(ManagedScopeBinding.IsOpenId(row));
        row.ApplicationRevision--;
        row.PolicySnapshot += Whitespace;
        Assert.False(ManagedScopeBinding.IsOpenId(row));
    }

    /// <summary>Duplicate, unknown, reordered, partial and version-substituted JSON remains invalid even if its attacker supplies a matching digest.</summary>
    [Theory]
    [InlineData(DuplicateDefect)]
    [InlineData(UnknownDefect)]
    [InlineData(WhitespaceDefect)]
    [InlineData(VersionDefect)]
    [InlineData(MissingDefect)]
    [InlineData(NullDefect)]
    [InlineData(DigestDefect)]
    public void Given_CanonicalPolicy_When_PersistedRepresentationIsSubstituted_Then_DecodeRejects(string kind)
    {
        var original = ManagedScopeCodec.Encode(ManagedScopeBinding.OpenId(Application, 1));
        var changed = kind switch
        {
            DuplicateDefect => original.Replace(OriginalVersion, DuplicateVersion, StringComparison.Ordinal),
            UnknownDefect => original.Insert(1, ExtraMember),
            WhitespaceDefect => original + Whitespace,
            VersionDefect => original.Replace(OriginalVersion, FutureVersion, StringComparison.Ordinal),
            MissingDefect => original.Replace(ScopesMember, string.Empty, StringComparison.Ordinal),
            NullDefect => original.Replace(EmptyScopes, NullScopes, StringComparison.Ordinal),
            _ => original
        };
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Decode(changed, kind == DigestDefect ? Binding : ManagedLoginRules.Digest(changed)));
    }

    /// <summary>Bounded requested sets preserve all 32 additional members, reject the 33rd and reject duplicate or non-subset grant membership.</summary>
    [Fact]
    public void Given_MaximumScopeDecision_When_MembershipChanges_Then_ExactFiniteSetsAreRequired()
    {
        var decision = Decision(MaximumScopes);
        var json = ManagedScopeCodec.Encode(decision);
        Assert.Equal(json, ManagedScopeCodec.Encode(ManagedScopeCodec.Decode(json, ManagedLoginRules.Digest(json))));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Encode(Decision(OverflowScopes)));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Encode(decision with { Granted = [OpenId, ScopeName] }));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Encode(decision with { Requested = [OpenId, OpenId] }));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Encode(decision with { Scopes = decision.Scopes.RemoveAt(0) }));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Encode(decision with { Audience = new(AudienceId, AudienceResource, 1, Binding) }));
    }

    /// <summary>Valid long native identities reach the exact byte boundary; one extra byte is rejected without relying on the SQL Server text storage width.</summary>
    [Fact]
    public void Given_ValidLargeSnapshot_When_EncodedSizeCrossesBoundary_Then_ExactByteLimitIsEnforced()
    {
        var decision = Decision(MaximumScopes);
        var scopes = decision.Scopes.ToArray();
        for (var index = 0; index < scopes.Length; index++)
        {
            scopes[index] = scopes[index] with
            {
                Id = new string('é', MultibyteIdLength) + index
            };
        }
        decision = decision with
        {
            Scopes = scopes.ToImmutableArray()
        };
        var baseline = ManagedScopeCodec.Encode(decision);
        var remaining = MaximumBytes - Encoding.UTF8.GetByteCount(baseline);
        Assert.InRange(remaining, 1, MaximumScopes * MaximumAsciiPadding);
        for (var index = 0; remaining > 0; index++)
        {
            var extra = Math.Min(remaining, MaximumAsciiPadding);
            scopes[index] = scopes[index] with
            {
                Id = scopes[index].Id + new string('x', extra)
            };
            remaining -= extra;
        }
        decision = decision with
        {
            Scopes = scopes.ToImmutableArray()
        };
        Assert.Equal(MaximumBytes, Encoding.UTF8.GetByteCount(ManagedScopeCodec.Encode(decision)));
        Assert.Throws<InvalidOperationException>(() => ManagedScopeCodec.Encode(decision with { ApplicationId = Application + AsciiPadding }));
    }

    /// <summary>Builds independent finite policy expectations using supported identity scopes and unique metadata digests.</summary>
    private static ManagedScopeDecision Decision(int count)
    {
        var entries = Enumerable.Range(0, count).Select(index => new ManagedScopeEntry(ScopeIdPrefix + index, ScopeNamePrefix + index.ToString(ScopeNumberFormat, System.Globalization.CultureInfo.InvariantCulture),
            1, IdentityKind, NoDisclosure, Binding)).ToImmutableArray();
        var names = entries.Select(value => value.Name).Append(OpenId).Order(StringComparer.Ordinal).ToImmutableArray();
        return new(1, Application, 1, Binding, names, [OpenId], entries, null);
    }
}
