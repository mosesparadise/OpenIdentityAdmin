using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Catalogue;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.Catalogue;

/// <summary>Checks exact hostile upstream projections independently of native API availability or browser-generated TypeScript assertions.</summary>
public sealed class CatalogueProjectionTests
{
    private const int InitialRevision = 1;
    private const int ChangedRevision = 2;
    private const int PageSize = 25;
    private const int WrongPageSize = 100;
    private const int PriorSecond = -1;
    private const int InsertOffset = 1;
    private const string Id = "example-audience";
    private const string ScopeId = "example-scope";
    private const string Resource = "orders-api";
    private const string Name = "orders.read";
    private const string Display = "Orders";
    private const string Owner = "Operations";
    private const string Consent = "Read orders";
    private const string ApiKind = "api";
    private const string None = "none";
    private const string Queued = "queued";
    private const string AudienceLocation = "/api/v1/audiences/" + Id;
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid JobId = Guid.Parse("12345678-1234-4234-8234-123456789abc");
    private static readonly AudienceInput Input = new(Resource, Display, Owner, true);
    private static readonly AudienceDetails Audience = new(Id, Resource, Display, Owner, true, CatalogueVersion.Create(CatalogueVersion.Audience, Id, InitialRevision), false, Now.AddMinutes(InitialRevision));
    private static readonly ScopeDetails Scope = new(ScopeId, Name, Display, ApiKind, true, Consent, Id, Resource, None, CatalogueVersion.Create(CatalogueVersion.Scope, ScopeId, InitialRevision), false, Now.AddMinutes(InitialRevision));

    private const string OtherOwner = "Other";
    private const string OtherResource = "other-api";
    private const string WrongLocation = "https://evil.test/";
    private const string OtherId = "other";
    private const string Email = "email";
    private const string UnknownKind = "unknown";
    private const string SystemScope = "openid";
    private const string PageMember = "\"page\":1";
    private const string DuplicatePage = "\"page\":1,\"page\":1";
    private const string PrivateMember = "\"privateApproval\":true,";
    private const string JobPrefix = "/api/v1/audiences/deletions/";
    private const string GuidFormat = "D";
    private const string ScopeKind = "scope";
    private const string UnknownState = "done";
    private const string Expired = "expired";
    private const string Rejected = "rejected";
    private const string NoFailure = "none";
    private const string Referenced = "referenced";

    /// <summary>Create output must correspond to every submitted field, exact revision-one identity and API location before narrowing its observation lifetime.</summary>
    [Fact]
    public void Given_CreateResponse_When_RequestAndOutputMatch_Then_ExactPolicyAndEarlierExpiryArePreserved()
    {
        var request = Request(CatalogueProxyAction.Create) with
        {
            Input = Input
        };
        var result = Parse(Audience, request, Audience.Version, AudienceLocation);
        Assert.Equal(Audience, Assert.IsType<AudienceDetails>(result.Output));
        Assert.Equal(Now, Assert.IsType<AudienceDetails>(result.Narrow(Now)).ExpiresAt);
        foreach (var value in new[] { Audience with { Owner = OtherOwner }, Audience with { Resource = OtherResource }, Audience with { Enabled = false }, Audience with { Version = CatalogueVersion.Create(CatalogueVersion.Audience, Id, ChangedRevision) } })
        {
            Assert.Throws<InvalidDataException>(() => Parse(value, request, value.Version, AudienceLocation));
        }
        Assert.Throws<InvalidDataException>(() => Parse(Audience, request, Audience.Version, WrongLocation));
        Assert.Throws<InvalidDataException>(() => Parse(Audience, request, null, AudienceLocation));
        Assert.Throws<InvalidDataException>(() => new CatalogueProxyResult(StatusCodes.Status503ServiceUnavailable).Narrow(Now));
    }

    /// <summary>Details and edit cannot change identity, retain the old edit tag or conceal invalid finite scope policy in a well-shaped DTO.</summary>
    [Fact]
    public void Given_ScopeOrAudienceDetails_When_IdentityPolicyOrVersionIsSubstituted_Then_ProjectionRejectsTheResponse()
    {
        var request = Request(CatalogueProxyAction.Details) with
        {
            Kind = CatalogueVersion.Scope,
            Id = ScopeId
        };
        var valid = Parse(Scope, request, Scope.Version);
        Assert.Equal(Now, Assert.IsType<ScopeDetails>(valid.Narrow(Now)).ExpiresAt);
        foreach (var value in new[] { Scope with { Id = OtherId }, Scope with { Resource = null }, Scope with { Disclosure = Email }, Scope with { Kind = UnknownKind }, Scope with { ConsentText = string.Empty } })
        {
            Assert.Throws<InvalidDataException>(() => Parse(value, request, value.Version));
        }
        request = request with
        {
            Input = new ScopeInput(Name, Display, ApiKind, false, Consent, Id, None)
        };
        Assert.Throws<InvalidDataException>(() => Parse(Scope, request, Scope.Version));
        var edit = Request(CatalogueProxyAction.Edit) with
        {
            Input = Input,
            Version = Audience.Version
        };
        Assert.Throws<InvalidDataException>(() => Parse(Audience, edit, Audience.Version));
        var updated = Audience with
        {
            Version = CatalogueVersion.Create(CatalogueVersion.Audience, Id, ChangedRevision)
        };
        Assert.Equal(updated, Parse(updated, edit, updated.Version).Output);
        Assert.Throws<InvalidDataException>(() => Parse(Audience with { Id = OtherId }, Request(CatalogueProxyAction.Details), Audience.Version));
    }

    /// <summary>List projections cannot silently alter requested pages, prefix matches, unique identifiers or exact JSON field cardinality.</summary>
    [Fact]
    public void Given_PageResponse_When_UpstreamChangesPagingOrMembers_Then_FiniteProjectionRejectsIt()
    {
        var request = Request(CatalogueProxyAction.List) with
        {
            Id = null,
            Query = ApplicationListQuery.Parse([])
        };
        var item = new CatalogueSummary(Id, Resource, Display, true);
        var page = new CataloguePage([item], InitialRevision, PageSize, false, Now.AddMinutes(InitialRevision));
        Assert.Equal(Now, Assert.IsType<CataloguePage>(Parse(page, request).Narrow(Now)).ExpiresAt);
        foreach (var value in new[] { page with { Page = ChangedRevision }, page with { PageSize = WrongPageSize }, page with { HasMore = true }, page with { Items = [item, item] }, page with { Items = [item with { Name = SystemScope }] } })
        {
            Assert.Throws<InvalidDataException>(() => Parse(value, request));
        }
        Assert.Throws<InvalidDataException>(() => Parse(page, request, Audience.Version));
        var bytes = Bytes(page);
        var duplicate = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(PageMember, DuplicatePage, StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => CatalogueProxyResult.Parse(duplicate, request, StatusCodes.Status200OK, null, null));
        var extra = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Insert(InsertOffset, PrivateMember));
        Assert.Throws<JsonException>(() => CatalogueProxyResult.Parse(extra, request, StatusCodes.Status200OK, null, null));
    }

    /// <summary>Durable jobs are bound to kind, original target and requested job identity; finite state/reason and admission location must agree.</summary>
    [Fact]
    public void Given_DurableStatus_When_JobIdentityOrTerminalMeaningChanges_Then_ProjectionRejectsTheSubstitution()
    {
        var job = new CatalogueDeletionDetails(JobId, CatalogueVersion.Audience, Id, Queued, None, Now, Now, Now.AddMinutes(InitialRevision));
        var request = Request(CatalogueProxyAction.Delete);
        var location = JobPrefix + JobId.ToString(GuidFormat);
        Assert.Equal(Now, Assert.IsType<CatalogueDeletionDetails>(Parse(job, request, location: location).Narrow(Now)).ExpiresAt);
        foreach (var value in new[] { job with { Id = Guid.Empty }, job with { TargetId = OtherId }, job with { Kind = ScopeKind }, job with { State = UnknownState }, job with { Failure = Expired }, job with { ObservedAt = Now.AddSeconds(PriorSecond) }, job with { State = Rejected, Failure = NoFailure } })
        {
            Assert.Throws<InvalidDataException>(() => Parse(value, request, location: location));
        }
        Assert.Throws<InvalidDataException>(() => Parse(job, request));
        var read = Request(CatalogueProxyAction.Status) with
        {
            Id = null,
            Job = JobId
        };
        Assert.Equal(job, Parse(job, read).Output);
        Assert.Throws<InvalidDataException>(() => Parse(job with { Id = Guid.NewGuid() }, read));
        Assert.Equal(Rejected, Assert.IsType<CatalogueDeletionDetails>(Parse(job with
        {
            State = Rejected,
            Failure = Referenced
        }, read).Output).State);
    }

    /// <summary>Builds a finite original request; tests explicitly alter independent response data and assert the production parser decision.</summary>
    private static CatalogueProxyRequest Request(CatalogueProxyAction action) => new(CatalogueVersion.Audience, action,
        action is CatalogueProxyAction.Create or CatalogueProxyAction.List ? null : Id, null, null, null, ReadOnlyMemory<byte>.Empty, null);

    /// <summary>Serializes public input only; production exact parsing still checks every wire member independently.</summary>
    private static byte[] Bytes(object value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonSerializerOptions.Web);

    /// <summary>Calls the real response projection without a transport mock supplying the acceptance decision.</summary>
    private static CatalogueProxyResult Parse(object value, CatalogueProxyRequest request, string? tag = null, string? location = null) =>
        CatalogueProxyResult.Parse(Bytes(value), request, StatusCodes.Status200OK, tag, location);
}
