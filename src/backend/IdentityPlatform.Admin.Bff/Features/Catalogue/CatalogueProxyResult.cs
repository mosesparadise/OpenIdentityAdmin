using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Admin.Bff.Features.Catalogue;

/// <summary>Validates finite upstream output against the exact request before any browser publication.</summary>
internal sealed record CatalogueProxyResult(int Status, object? Output = null, string? Version = null, DateTimeOffset? ExpiresAt = null)
{
    private const string Queued = "queued";
    private const string Succeeded = "succeeded";
    private const string Rejected = "rejected";
    private const string None = "none";
    private const string Stale = "stale";
    private const string Referenced = "referenced";
    private const string Authority = "authority";
    private const string Expired = "expired";
    private const string Missing = "missing";

    /// <summary>Selects only the request's expected DTO, current resource identity and exact response framing metadata.</summary>
    internal static CatalogueProxyResult Parse(byte[] bytes, CatalogueProxyRequest input, int status, string? tag, string? location)
    {
        if (input.Action == CatalogueProxyAction.List)
        {
            var page = ApplicationJson.Parse<CataloguePage>(bytes);
            return Page(page, input.Query!, input.Kind) && tag is null && location is null
                ? new(status, page, null, page.ExpiresAt) : throw new InvalidDataException();
        }
        if (input.Action is CatalogueProxyAction.Delete or CatalogueProxyAction.Status)
        {
            var job = ApplicationJson.Parse<CatalogueDeletionDetails>(bytes);
            if (!Job(job, input))
            {
                throw new InvalidDataException();
            }
            var expected = input.Action == CatalogueProxyAction.Delete ? CataloguePaths.ApiRoot + CataloguePaths.Job(input.Kind, job.Id) : null;
            return tag is null && location == expected ? new(status, job, null, job.ExpiresAt) : throw new InvalidDataException();
        }
        return Details(bytes, input, status, tag, location);
    }

    /// <summary>Checks finite positive catalogue identity and input correspondence before accepting an opaque server version.</summary>
    private static CatalogueProxyResult Details(byte[] bytes, CatalogueProxyRequest input, int status, string? tag, string? location)
    {
        if (!CatalogueVersion.IsCanonical(tag))
        {
            throw new InvalidDataException();
        }
        string id;
        string version;
        DateTimeOffset expiry;
        object output;
        if (input.Kind == CatalogueVersion.Scope)
        {
            var scope = ApplicationJson.Parse<ScopeDetails>(bytes);
            if (!Scope(scope) || input.Input is ScopeInput expected && !Matches(scope, expected))
            {
                throw new InvalidDataException();
            }
            (id, version, expiry, output) = (scope.Id, scope.Version, scope.ExpiresAt, scope);
        }
        else
        {
            var audience = ApplicationJson.Parse<AudienceDetails>(bytes);
            if (!CatalogueRules.IsIdentifier(audience.Id) || CatalogueRules.Validate(new AudienceInput(audience.Resource, audience.DisplayName, audience.Owner, audience.Enabled)).Count != 0 ||
                input.Input is AudienceInput expected && new AudienceInput(audience.Resource, audience.DisplayName, audience.Owner, audience.Enabled) != expected)
            {
                throw new InvalidDataException();
            }
            (id, version, expiry, output) = (audience.Id, audience.Version, audience.ExpiresAt, audience);
        }
        var created = input.Action == CatalogueProxyAction.Create;
        if (tag != version || !CataloguePaths.IsTarget(id) || !created && id != input.Id ||
            created && tag != CatalogueVersion.Create(input.Kind, id, 1) || input.Action == CatalogueProxyAction.Edit && tag == input.Version ||
            location != (created ? CataloguePaths.ApiRoot + CataloguePaths.Detail(input.Kind, id) : null))
        {
            throw new InvalidDataException();
        }
        return new(status, output, tag, expiry);
    }

    /// <summary>Validates exact finite scope/audience combinations without interpreting resource strings as URLs to call.</summary>
    private static bool Scope(ScopeDetails value) => CatalogueRules.IsIdentifier(value.Id) &&
        CatalogueRules.Validate(new ScopeInput(value.Name, value.DisplayName, value.Kind, value.Enabled, value.ConsentText, value.AudienceId, value.Disclosure)).Count == 0 &&
        (value.Kind == CatalogueRules.Api ? CatalogueRules.IsResource(value.Resource) : value.Resource is null);

    /// <summary>Requires every submitted policy field to survive the API mutation unchanged.</summary>
    private static bool Matches(ScopeDetails value, ScopeInput input) => new ScopeInput(value.Name, value.DisplayName, value.Kind,
        value.Enabled, value.ConsentText, value.AudienceId, value.Disclosure) == input;

    /// <summary>Requires requested paging and bounded unique rows without accepting arbitrary additional JSON fields.</summary>
    private static bool Page(CataloguePage page, ApplicationListQuery query, string kind) => page.Page == query.Page && page.PageSize == query.PageSize &&
        page.Items is not null && page.Items.Count <= query.PageSize && (!page.HasMore || page.Items.Count == query.PageSize) &&
        page.Items.All(row => row is not null && CataloguePaths.IsTarget(row.Id) &&
            (kind == CatalogueVersion.Scope ? CatalogueRules.IsName(row.Name) : CatalogueRules.IsResource(row.Name)) &&
            row.Name.StartsWith(query.Search, StringComparison.Ordinal) && ApplicationEditorRules.IsText(row.DisplayName, CatalogueRules.DisplayLength)) &&
        page.Items.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() == page.Items.Count;

    /// <summary>Checks request-bound durable status, coherent finite terminal reasons and monotonic observation dates.</summary>
    private static bool Job(CatalogueDeletionDetails job, CatalogueProxyRequest input) => job.Id != Guid.Empty && job.Kind == input.Kind &&
        CataloguePaths.IsTarget(job.TargetId) && (input.Id is null || input.Id == job.TargetId) && (input.Job is null || input.Job == job.Id) &&
        job.CreatedAt >= DateTimeOffset.UnixEpoch && job.ObservedAt >= job.CreatedAt &&
        (job.State is Queued or Succeeded ? job.Failure == None : job.State == Rejected && job.Failure is Stale or Referenced or Authority or Expired or Missing);

    /// <summary>Shortens only the returned observation expiry; no original source/session lifetime can be renewed by upstream data.</summary>
    internal object Narrow(DateTimeOffset expiry) => Output switch
    {
        CataloguePage page => page with { ExpiresAt = expiry },
        ScopeDetails scope => scope with { ExpiresAt = expiry },
        AudienceDetails audience => audience with { ExpiresAt = expiry },
        CatalogueDeletionDetails job => job with { ExpiresAt = expiry },
        _ => throw new InvalidDataException()
    };
}
