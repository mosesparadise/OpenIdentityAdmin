using System.Globalization;
using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Defines independent response mutations and exact limits for the four genuine grant transport shapes.</summary>
internal static class GrantTransportFaults
{
    internal const string Hold = "hold";
    internal const string Abort = "abort";
    internal const string StreamOverflow = "stream-overflow";
    internal const string LaterExpiry = "later-expiry";
    internal const string LargeCounts = "large-counts";
    internal const string ExactBytes = "exact-bytes";
    private const string BeyondSafeInteger = "9007199254740993";
    private const string MaximumCount = "9223372036854775807";
    private const string Tokens = "tokenCount";
    private const string Redirect = "redirect";
    private const string Html = "html";
    private const string Encoding = "encoding";
    private const string Tag = "tag";
    private const string Location = "location";
    private const string Malformed = "malformed";
    private const string Duplicate = "duplicate";
    private const string Unknown = "unknown";
    private const string Overflow = "overflow";
    private const int MaximumBytes = 524288;
    private const string OtherJob = "11111111-1111-4111-8111-111111111111";
    private const string OtherApplication = "other-application";
    private const string Expires = "expiresAt";
    private const string Items = "items";
    private const string Job = "job";
    private const string State = "state";
    private const string Failure = "failure";
    private const string Count = "authorizationCount";
    private const string Version = "version";
    private const string ApplicationFault = "application";
    private const string JobIdentityFault = "job-id";
    private const string VersionFault = "version";
    private const string StateFault = "state";
    private const string FailureFault = "failure";
    private const string NegativeFault = "negative";
    private const string LeadingZeroFault = "leading-zero";
    private const string NumericFault = "numeric";
    private const string CountOverflowFault = "count-overflow";
    private const string FractionalTimeFault = "fractional-time";
    private const string TimeOrderFault = "time-order";
    private const string KeyFault = "key";
    private const string DuplicateJobFault = "duplicate-job";
    private const string TooManyFault = "too-many";
    private const string NullItemsFault = "null-items";
    private const string PageExpiryFault = "page-expiry";
    private const string ExpiredFault = "expired";
    private const string ApplicationId = "applicationId";
    private const string Id = "id";
    private const string UnknownState = "unknown";
    private const string StorageFailure = "storage";
    private const string NegativeCount = "-1";
    private const string LeadingZeroCount = "01";
    private const string OverflowCount = "9223372036854775808";
    private const string Created = "createdAt";
    private const string Updated = "updatedAt";
    private const string RequestKey = "requestKey";
    private const string GuidFormat = "D";
    private const string HtmlMedia = "text/html";
    private const string Compression = "gzip";
    private const string ForeignLocation = "https://foreign.example.test/private";
    private const string ObjectStart = "{";
    private const string ApplicationMember = "\"applicationId\":";
    private const string DuplicateMember = "\"applicationId\":\"other\",\"applicationId\":";
    private const string PrivateMember = "\"private\":\"";
    private const string MemberEnd = "\",";
    internal static readonly int[] Statuses = [400, 401, 403, 404, 409, 412, 428, 429, 500, 503];
    internal static readonly string[] Common = [Malformed, Duplicate, Unknown, Overflow, StreamOverflow, Html, Encoding, Redirect, Abort,
        ApplicationFault, VersionFault, StateFault, FailureFault, NegativeFault, LeadingZeroFault, NumericFault, CountOverflowFault, FractionalTimeFault, TimeOrderFault, ExpiredFault];

    /// <summary>Only meaningful bindings are mutated for each route; page identity and cardinality are tested separately.</summary>
    internal static IEnumerable<string> For(string shape) => Common.Concat(shape switch
    {
        GrantTransportContext.Recent => [DuplicateJobFault, TooManyFault, PageExpiryFault, NullItemsFault],
        GrantTransportContext.Exact => [Tag, JobIdentityFault],
        GrantTransportContext.Continue => [Tag, Location, KeyFault, JobIdentityFault],
        _ => [Tag, Location, KeyFault]
    });

    /// <summary>Controls transport metadata independently of API semantics, leaving the original committed operation intact.</summary>
    internal static string Apply(HttpContext http, string body, string fault)
    {
        if (int.TryParse(fault, CultureInfo.InvariantCulture, out var status))
        {
            http.Response.StatusCode = status;
            return GrantTransportProbe.Canary;
        }
        switch (fault)
        {
            case Html:
                http.Response.ContentType = HtmlMedia;
                break;
            case Encoding:
                http.Response.Headers.ContentEncoding = Compression;
                break;
            case Tag:
                http.Response.Headers.ETag = ApplicationVersion.Create(OtherJob, 1);
                break;
            case Location:
                http.Response.Headers.Location = ForeignLocation;
                break;
            case Redirect:
                http.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                http.Response.Headers.Location = GrantTransportProbe.Redirect;
                break;
            case Malformed:
                return ObjectStart;
            case Duplicate:
                return body.Replace(ApplicationMember, DuplicateMember, StringComparison.Ordinal);
            case Unknown:
                return body.Insert(1, PrivateMember + GrantTransportProbe.Canary + MemberEnd);
            case Overflow or StreamOverflow:
                return body.PadRight(MaximumBytes + 1);
            case ExactBytes:
                var padded = body.PadRight(MaximumBytes);
                Assert.Equal(MaximumBytes, System.Text.Encoding.UTF8.GetByteCount(padded));
                return padded;
        }
        var result = Mutate(body, fault);
        BindHeaders(http, fault, result);
        return result;
    }

    /// <summary>Keeps transport headers coherent with substituted bodies so independent job and version guards cannot be masked by header mismatches.</summary>
    private static void BindHeaders(HttpContext http, string fault, string body)
    {
        if (fault is not (JobIdentityFault or VersionFault))
        {
            return;
        }
        var root = JsonNode.Parse(body)!;
        var job = root[Job] ?? root;
        if (root[Items] is not null)
        {
            return;
        }
        http.Response.Headers.ETag = job[Version]!.GetValue<string>();
        if (fault == JobIdentityFault && root[Job] is not null)
        {
            var location = http.Response.Headers.Location.ToString();
            http.Response.Headers.Location = location[..(location.LastIndexOf('/') + 1)] + OtherJob;
        }
    }

    /// <summary>Changes a genuine job or page with strict finite-value and binding negatives; unrelated fields remain valid.</summary>
    private static string Mutate(string body, string fault)
    {
        var root = JsonNode.Parse(body)!;
        var job = root[Job] ?? (root[Items] is JsonArray items ? items[0] : root);
        MutateJob(job!, fault);
        MutateEnvelope(root, job!, fault);
        return root.ToJsonString();
    }

    /// <summary>Targets one independently valid job's identity, finite state, lossless count or ordered whole-second timestamps.</summary>
    private static void MutateJob(JsonNode job, string fault)
    {
        switch (fault)
        {
            case ApplicationFault:
                job![ApplicationId] = OtherApplication;
                break;
            case JobIdentityFault:
                job![Id] = OtherJob;
                job[Version] = ApplicationVersion.Create(OtherJob, 1);
                break;
            case VersionFault:
                job![Version] = ApplicationVersion.Create(OtherJob, 1);
                break;
            case StateFault:
                job![State] = UnknownState;
                break;
            case FailureFault:
                job![Failure] = StorageFailure;
                break;
            case NegativeFault:
                job![Count] = NegativeCount;
                break;
            case LeadingZeroFault:
                job![Count] = LeadingZeroCount;
                break;
            case NumericFault:
                job![Count] = 1;
                break;
            case CountOverflowFault:
                job![Count] = OverflowCount;
                break;
            case FractionalTimeFault:
                job![Updated] = job[Updated]!.GetValue<DateTimeOffset>().AddTicks(1);
                break;
            case TimeOrderFault:
                job![Updated] = job[Created]!.GetValue<DateTimeOffset>().AddSeconds(-1);
                break;
        }
    }

    /// <summary>Changes only acceptance or collection structure and coherent response expiry, preserving the nested original job otherwise.</summary>
    private static void MutateEnvelope(JsonNode root, JsonNode job, string fault)
    {
        switch (fault)
        {
            case KeyFault:
                root[RequestKey] = OtherJob;
                break;
            case DuplicateJobFault:
                root[Items]!.AsArray().Add(job!.DeepClone());
                break;
            case TooManyFault:
                Fill(root[Items]!.AsArray());
                break;
            case NullItemsFault:
                root[Items] = null;
                break;
            case PageExpiryFault:
                root[Expires] = root[Expires]!.GetValue<DateTimeOffset>().AddSeconds(1);
                break;
            case ExpiredFault:
                SetExpiry(root, DateTimeOffset.UnixEpoch);
                break;
            case LaterExpiry:
                SetExpiry(root, job![Expires]!.GetValue<DateTimeOffset>().AddHours(1));
                break;
            case LargeCounts:
                job[Count] = BeyondSafeInteger;
                job[Tokens] = MaximumCount;
                break;
        }
    }

    /// <summary>Overflows the ten-item boundary with eleven otherwise independently valid unique jobs.</summary>
    private static void Fill(JsonArray items)
    {
        const int TooMany = 11;
        while (items.Count < TooMany)
        {
            var job = items[0]!.DeepClone();
            var id = Guid.NewGuid().ToString(GuidFormat);
            job[Id] = id;
            job[Version] = ApplicationVersion.Create(id, 1);
            items.Add(job);
        }
    }

    /// <summary>Keeps page/item deadlines coherent so rejection or clamping isolates original authority rather than a malformed page.</summary>
    private static void SetExpiry(JsonNode root, DateTimeOffset expiry)
    {
        if (root[Items] is JsonArray items)
        {
            root[Expires] = expiry;
            foreach (var item in items)
            {
                item![Expires] = expiry;
            }
        }
        else
        {
            (root[Job] ?? root)[Expires] = expiry;
        }
    }
}
