using IdentityPlatform.Administrator.Tests.Features.Browser;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Seeds cross-resource, stale-version and noncanonical-secret defects into the actual passive browser binding oracle.</summary>
public sealed class ApplicationEditorBindingTests
{
    private const string Identifier = "created-application";
    private const string Collection = "/bff/api/v1/applications/";
    private const string Created = "\"4e9ddb70dba65507019b3993220d64eff5ac2860dacf488837af18d2c6bdf961-1\"";
    private const string Edited = "\"4e9ddb70dba65507019b3993220d64eff5ac2860dacf488837af18d2c6bdf961-2\"";
    private const string Secret = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private const string Alias = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB=";
    private const string Private = "private-observation-canary";
    private const string Query = "?private=";
    private const string Fragment = "#private=";
    private const string Weak = "W/";
    private const string EncodedIdentifier = "%63reated-application";
    private static readonly Uri Origin = new("https://bff.example.test/");
    private static readonly Uri OtherOrigin = new("https://other.example.test/");

    /// <summary>Only the original resource and first successor bind; different origins, IDs, aliases, query material and stale tags fail privately.</summary>
    [Fact, Trait("Category", "Harness")]
    public void Given_OriginalCreationVersion_When_ObservedEditTargetsDrift_Then_OnlyExactResourceAndSuccessorBind()
    {
        var target = new Uri(Origin, Collection + Identifier).AbsoluteUri;
        Assert.Equal(Identifier, ApplicationEditorBinding.CreatedIdentity(Origin, target, Created, Created, Edited));
        foreach (var altered in new[] { target + Query + Private, target + Fragment + Private,
            new Uri(Origin, Collection + Private).AbsoluteUri, Origin.GetLeftPart(UriPartial.Authority) + Collection + EncodedIdentifier,
            new Uri(OtherOrigin, Collection + Identifier).AbsoluteUri })
        {
            var error = Assert.Throws<Xunit.Sdk.TrueException>(() => ApplicationEditorBinding.CreatedIdentity(Origin, altered, Created, Created, Edited));
            Assert.DoesNotContain(Private, error.ToString(), StringComparison.Ordinal);
        }
        Assert.Throws<Xunit.Sdk.TrueException>(() => ApplicationEditorBinding.CreatedIdentity(Origin, target, Created, Weak + Created, Edited));
        Assert.Throws<Xunit.Sdk.TrueException>(() => ApplicationEditorBinding.CreatedIdentity(Origin, target, Created, Created, Created));
    }

    /// <summary>A visible length alone cannot satisfy credential evidence; noncanonical trailing bits, invalid alphabet and wrong size fail without printing material.</summary>
    [Fact, Trait("Category", "Harness")]
    public void Given_VisibleCredential_When_EncodingIsAliasedOrMalformed_Then_OnlyCanonicalNativeMaterialIsAccepted()
    {
        ApplicationEditorBinding.Secret(Secret);
        foreach (var value in new[] { Alias, Private, Secret + Private, Secret[..^1] })
        {
            var error = Assert.Throws<Xunit.Sdk.TrueException>(() => ApplicationEditorBinding.Secret(value));
            Assert.DoesNotContain(value, error.ToString(), StringComparison.Ordinal);
        }
    }
}
