using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks non-authoritative pair representation, raw cardinality and independent decoded-byte digest vectors.</summary>
public sealed class BrowserLoginBindingTests
{
    internal const string Intent = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8";
    internal const string Completion = "ICEiIyQlJicoKSorLC0uLzAxMjM0NTY3ODk6Ozw9Pj8";
    internal const string Other = "QEFCQ0RFRkdISUpLTE1OT1BRUlNUVVZXWFlaW1xdXl8";
    internal const string Digest = "630DCD2966C4336691125448BBB25B4FF412A49C732DB2C8ABC1B8581BD710DD";
    private const string IntentName = "admin_login_intent";
    private const string CompletionName = "admin_login_completion";
    private const string PrivateIntent = "oi_b3_intent_selector";
    private const string PrivateCompletion = "oi_b3_completion_selector";
    private const string MissingFault = "missing";
    private const string EmptyFault = "empty";
    private const string EmptyArrayFault = "empty-array";
    private const string ArrayFault = "array";
    private const string DuplicateFault = "duplicate";
    private const string AliasFault = "alias";
    private const string AliasExtraFault = "alias-extra";
    private const string ShortFault = "short";
    private const string LongFault = "long";
    private const string PaddingFault = "padding";
    private const string PadBitsFault = "pad-bits";
    private const string SpaceFault = "space";
    private const string UnicodeFault = "unicode";
    private const string StandardAlphabetFault = "standard-alphabet";
    private const string TypeFault = "type";
    private const string PartialFault = "partial";
    private const string IdentityFault = "identity";
    private const string ActorFault = "actor";
    private const string NoncanonicalPadBits = "9";
    private const string ExtraCharacter = "A";
    private const string PaddingCharacter = "=";
    private const string SpaceCharacter = " ";
    private const string UnicodeCharacter = "é";
    private const string StandardAlphabetCharacter = "+";
    private const string PrivateAlias = "OI_B3_INTENT_SELECTOR";

    /// <summary>The published SHA-256 vector hashes bytes 0 through 31 and remains private representation rather than identity authority.</summary>
    [Fact]
    public void Given_CanonicalSelectors_When_AllRepresentationsAreRead_Then_DecodedIntentDigestAndOriginalSnapshotAgree()
    {
        Assert.Equal(BrowserLoginBindingState.Present, BrowserLoginBinding.Read(Fields(), out var raw));
        Assert.Equal(BrowserLoginBindingState.Present, BrowserLoginBinding.Read(Request(), out var native));
        Assert.Equal(Digest, raw!.IntentDigest);
        Assert.Equal(Digest, native!.IntentDigest);
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        raw.CopyTo(principal);
        Assert.Throws<InvalidOperationException>(() => raw.CopyTo(principal));
        Assert.True(raw.Matches(principal));
        Assert.All(principal.Claims, claim => Assert.Empty(claim.GetDestinations()));
        Assert.False(principal.Identity!.IsAuthenticated);
        Assert.DoesNotContain(principal.Claims, claim => claim.Type == OpenIddictConstants.Claims.Subject);
        Assert.DoesNotContain(Intent, raw.ToString()!, StringComparison.Ordinal);
        principal.SetClaim(PrivateCompletion, Other);
        Assert.False(raw.Matches(principal));
    }

    /// <summary>Only total absence denotes ordinary B2; a present empty or partial pair is malformed.</summary>
    [Fact]
    public void Given_NoPair_When_RawNativeAndPrivateRepresentationsAreRead_Then_ExplicitAbsenceIsReturned()
    {
        Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read([], out var raw));
        Assert.Null(raw);
        Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read(new OpenIddictRequest(), out _));
        Assert.Equal(BrowserLoginBindingState.Absent, BrowserLoginBinding.Read(new ClaimsPrincipal(new ClaimsIdentity()), out _));
    }

    /// <summary>Each field independently rejects bounded but noncanonical selector encodings and incomplete or ambiguous raw representation.</summary>
    [Theory]
    [InlineData(MissingFault)]
    [InlineData(EmptyFault)]
    [InlineData(EmptyArrayFault)]
    [InlineData(ArrayFault)]
    [InlineData(DuplicateFault)]
    [InlineData(AliasFault)]
    [InlineData(AliasExtraFault)]
    [InlineData(ShortFault)]
    [InlineData(LongFault)]
    [InlineData(PaddingFault)]
    [InlineData(PadBitsFault)]
    [InlineData(SpaceFault)]
    [InlineData(UnicodeFault)]
    [InlineData(StandardAlphabetFault)]
    public void Given_RawPair_When_OneFieldIsMalformed_Then_PresenceCannotDowngradeToB2(string fault)
    {
        foreach (var name in new[] { IntentName, CompletionName })
        {
            var fields = Fields();
            Alter(fields, name, fault);
            Assert.Equal(BrowserLoginBindingState.Invalid, BrowserLoginBinding.Read(fields, out var binding));
            Assert.Null(binding);
        }
    }

    /// <summary>Native arrays, objects and numeric values cannot be accepted through scalar convenience conversion.</summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("[\"AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8\"]")]
    [InlineData("{}")]
    [InlineData("42")]
    [InlineData("true")]
    public void Given_NativePair_When_AParameterIsNotAJsonString_Then_Denied(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var name in new[] { IntentName, CompletionName })
        {
            var request = Request();
            request.SetParameter(name, document.RootElement.Clone());
            Assert.Equal(BrowserLoginBindingState.Invalid, BrowserLoginBinding.Read(request, out _));
        }
    }

    /// <summary>Private data is uniquely typed and attached to one actor-free identity; it cannot be silently overwritten.</summary>
    [Theory]
    [InlineData(DuplicateFault)]
    [InlineData(AliasFault)]
    [InlineData(TypeFault)]
    [InlineData(PartialFault)]
    [InlineData(IdentityFault)]
    [InlineData(ActorFault)]
    [InlineData(PadBitsFault)]
    public void Given_PrivatePair_When_RepresentationChanges_Then_OriginalSnapshotAndOverwriteChecksRejectIt(string fault)
    {
        Assert.Equal(BrowserLoginBindingState.Present, BrowserLoginBinding.Read(Fields(), out var binding));
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        binding!.CopyTo(principal);
        AlterPrivate(principal, identity, fault);
        Assert.Equal(BrowserLoginBindingState.Invalid, BrowserLoginBinding.Read(principal, out _));
        Assert.False(binding.Matches(principal));
        Assert.Throws<InvalidOperationException>(() => binding.CopyTo(principal));
    }

    /// <summary>Changes exactly the selected private representation dimension while retaining its canonical companion.</summary>
    private static void AlterPrivate(ClaimsPrincipal principal, ClaimsIdentity identity, string fault)
    {
        switch (fault)
        {
            case DuplicateFault:
                identity.AddClaim(new(PrivateIntent, Intent));
                break;
            case AliasFault:
                identity.AddClaim(new(PrivateAlias, Intent));
                break;
            case TypeFault:
                identity.RemoveClaim(identity.FindFirst(PrivateIntent)!);
                identity.AddClaim(new(PrivateIntent, Intent, ClaimValueTypes.Integer));
                break;
            case PartialFault:
                principal.SetClaim(PrivateCompletion, (string?)null);
                break;
            case IdentityFault:
                principal.AddIdentity(new ClaimsIdentity());
                break;
            case ActorFault:
                identity.Actor = new ClaimsIdentity();
                break;
            case PadBitsFault:
                principal.SetClaim(PrivateIntent, Intent[..^1] + NoncanonicalPadBits);
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>Builds exact synthetic fields without using the production selector generator or digest implementation.</summary>
    internal static List<KeyValuePair<string, StringValues>> Fields() => [new(IntentName, Intent), new(CompletionName, Completion)];

    /// <summary>Supplies the identical data to native parameter storage for independent shape checks.</summary>
    internal static OpenIddictRequest Request() => new() { [IntentName] = Intent, [CompletionName] = Completion };

    /// <summary>Substitutes exactly one dimension, retaining an otherwise canonical companion selector.</summary>
    private static void Alter(List<KeyValuePair<string, StringValues>> fields, string name, string fault)
    {
        var index = fields.FindIndex(pair => pair.Key == name);
        var value = fields[index].Value[0]!;
        switch (fault)
        {
            case MissingFault:
                fields.RemoveAt(index);
                return;
            case DuplicateFault:
                fields.Add(new(name, value));
                return;
            case AliasExtraFault:
                fields.Add(new(name.ToUpperInvariant(), value));
                return;
            case AliasFault:
                fields[index] = new(name.ToUpperInvariant(), value);
                return;
            case EmptyArrayFault:
                fields[index] = new(name, StringValues.Empty);
                return;
            case ArrayFault:
                fields[index] = new(name, new StringValues([value, value]));
                return;
        }
        value = fault switch
        {
            EmptyFault => string.Empty,
            ShortFault => value[..^1],
            LongFault => value + ExtraCharacter,
            PaddingFault => value + PaddingCharacter,
            PadBitsFault => value[..^1] + NoncanonicalPadBits,
            SpaceFault => SpaceCharacter + value[1..],
            UnicodeFault => UnicodeCharacter + value[1..],
            StandardAlphabetFault => StandardAlphabetCharacter + value[1..],
            _ => throw new InvalidOperationException()
        };
        fields[index] = new(name, value);
    }
}
