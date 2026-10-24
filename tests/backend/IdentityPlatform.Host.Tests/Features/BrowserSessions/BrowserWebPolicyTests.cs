using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks pure browser policy without treating predicates as installed route or streaming-body enforcement.</summary>
public sealed class BrowserWebPolicyTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Client = "https://admin.example.test/";
    private const string Origin = "https://admin.example.test";
    private const string Field = "Action";
    private const string Value = "start";
    private const string Token = "synthetic-native-token";
    private const string OriginHeader = "Origin";
    private const string Bearer = "Authorization";
    private const string AttackerHost = "evil.example.test";
    private const string Multipart = "multipart/form-data";
    private const string ExtraQuery = "?return=elsewhere";
    private const string ReturnField = "return";
    private const string AliasField = "action";
    private const int OversizeBody = 8193;
    private const int OversizeValue = 4097;
    private const string Media = "application/x-www-form-urlencoded";

    /// <summary>Fixed destinations cannot be redirected through noncanonical configuration or paths masquerading as origins.</summary>
    [Theory]
    [InlineData("http://admin.example.test/")]
    [InlineData("https://admin.example.test")]
    [InlineData("https://ADMIN.example.test/")]
    [InlineData("https://admin.example.test:443/")]
    [InlineData("https://user@admin.example.test/")]
    [InlineData("https://admin.example.test/?return=elsewhere")]
    [InlineData("https://admin.example.test/#fragment")]
    public void Given_InvalidConfiguredRoot_When_DestinationsAreCreated_Then_FailureContainsNoSuppliedValue(string client)
    {
        var error = Assert.Throws<ArgumentException>(() => new BrowserSessionDestinations(Issuer, client));
        Assert.DoesNotContain(client, error.Message, StringComparison.Ordinal);
    }

    /// <summary>Only reviewed root-relative destinations and authority are derived from configuration.</summary>
    [Fact]
    public void Given_CanonicalRoots_When_DestinationsAreRead_Then_OnlyFixedPathsAreAvailable()
    {
        var policy = new BrowserSessionDestinations(Issuer, Client);
        Assert.Equal("https://issuer.example.test/account/administrator/browser/login", policy.Entry.AbsoluteUri);
        Assert.Equal("https://admin.example.test/bff/login/resume", policy.Resume.AbsoluteUri);
        Assert.Equal("https://admin.example.test/bff/logout/return", policy.LogoutReturn.AbsoluteUri);
        Assert.Equal(Origin, policy.Origin);
    }

    /// <summary>Preserves native canonical identity URI support; fixed paths do not require different origins or a root-only identity URI.</summary>
    [Theory]
    [InlineData("https://issuer.example.test/", "https://issuer.example.test/bff/login/resume")]
    [InlineData("https://admin.example.test/application/", "https://admin.example.test/bff/login/resume")]
    public void Given_SupportedNativeIdentityUri_When_BrowserDestinationsAreBuilt_Then_FixedPathsRemainConfigured(string client, string expected)
    {
        Assert.Equal(expected, new BrowserSessionDestinations(Issuer, client).Resume.AbsoluteUri);
    }

    /// <summary>Origin is ordinal, single and configured; request Host cannot authorize an attacker-supplied origin.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("null", false)]
    [InlineData("https://admin.example.test", true)]
    [InlineData("https://admin.example.test/", false)]
    [InlineData("https://ADMIN.example.test", false)]
    [InlineData("https://evil.example.test", false)]
    [InlineData("https://admin.example.test, https://evil.example.test", false)]
    public void Given_OriginHeader_When_MutationIsClassified_Then_OnlyExactConfiguredOriginPasses(string? origin, bool expected)
    {
        var request = Request();
        request.Host = new HostString(AttackerHost);
        request.Headers[OriginHeader] = origin;
        Assert.Equal(expected, BrowserRequestPolicy.Mutation(request, new(Issuer, Client)));
    }

    /// <summary>Metadata rejects alternate verbs/media, oversized declared length, queries and even empty bearer substitution headers.</summary>
    [Theory]
    [InlineData(BrowserMetadataFault.Get)]
    [InlineData(BrowserMetadataFault.Http)]
    [InlineData(BrowserMetadataFault.Media)]
    [InlineData(BrowserMetadataFault.Length)]
    [InlineData(BrowserMetadataFault.Query)]
    [InlineData(BrowserMetadataFault.Bearer)]
    [InlineData(BrowserMetadataFault.Origins)]
    public void Given_InvalidMutationMetadata_When_Checked_Then_DeniedBeforeParsing(BrowserMetadataFault fault)
    {
        var request = Request();
        switch (fault)
        {
            case BrowserMetadataFault.Get:
                request.Method = HttpMethods.Get;
                break;
            case BrowserMetadataFault.Http:
                request.Scheme = Uri.UriSchemeHttp;
                break;
            case BrowserMetadataFault.Media:
                request.ContentType = Multipart;
                break;
            case BrowserMetadataFault.Length:
                request.ContentLength = OversizeBody;
                break;
            case BrowserMetadataFault.Query:
                request.QueryString = new QueryString(ExtraQuery);
                break;
            case BrowserMetadataFault.Bearer:
                request.Headers[Bearer] = string.Empty;
                break;
            case BrowserMetadataFault.Origins:
                request.Headers[OriginHeader] = new StringValues([Origin, Origin]);
                break;
        }
        Assert.False(BrowserRequestPolicy.Mutation(request, new(Issuer, Client)));
    }

    /// <summary>Decoded form shape rejects duplicate, extra, differently cased, missing and oversized values without concatenating them.</summary>
    [Theory]
    [InlineData(BrowserFormFault.Valid, true)]
    [InlineData(BrowserFormFault.Duplicate, false)]
    [InlineData(BrowserFormFault.Extra, false)]
    [InlineData(BrowserFormFault.Case, false)]
    [InlineData(BrowserFormFault.Missing, false)]
    [InlineData(BrowserFormFault.Oversize, false)]
    [InlineData(BrowserFormFault.File, false)]
    public void Given_DecodedForm_When_ShapeIsChecked_Then_ExactSingleBoundedFieldsAreRequired(BrowserFormFault fault, bool expected)
    {
        var values = new Dictionary<string, StringValues>(StringComparer.Ordinal) { [Field] = Value, [BrowserRequestPolicy.AntiforgeryField] = Token };
        switch (fault)
        {
            case BrowserFormFault.Duplicate:
                values[Field] = new StringValues([Value, Value]);
                break;
            case BrowserFormFault.Extra:
                values[ReturnField] = Value;
                break;
            case BrowserFormFault.Case:
                values.Remove(Field);
                values[AliasField] = Value;
                break;
            case BrowserFormFault.Missing:
                values.Remove(BrowserRequestPolicy.AntiforgeryField);
                break;
            case BrowserFormFault.Oversize:
                values[Field] = new string('a', OversizeValue);
                break;
        }
        var files = new FormFileCollection();
        if (fault == BrowserFormFault.File)
        {
            files.Add(new FormFile(Stream.Null, 0, 0, Field, Value));
        }
        Assert.Equal(expected, BrowserRequestPolicy.Form(new FormCollection(values, files), Field));
    }

    /// <summary>Constructs valid metadata independently of the production predicate.</summary>
    private static HttpRequest Request()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = Uri.UriSchemeHttps;
        request.Method = HttpMethods.Post;
        request.ContentType = Media;
        request.Headers[OriginHeader] = Origin;
        return request;
    }
}

/// <summary>Enumerates independent transport metadata denials.</summary>
public enum BrowserMetadataFault
{
    Get, Http, Media, Length, Query, Bearer, Origins
}

/// <summary>Enumerates independent decoded form-shape denials.</summary>
public enum BrowserFormFault
{
    Valid, Duplicate, Extra, Case, Missing, Oversize, File
}
