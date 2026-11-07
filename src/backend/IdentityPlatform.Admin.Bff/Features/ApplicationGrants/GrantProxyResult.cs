using System.Globalization;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.ApplicationGrants;

/// <summary>Holds only strictly checked safe job data and one finite downstream status.</summary>
internal sealed record GrantProxyResult(int Status, GrantJobView? Job = null, GrantJobPage? Page = null)
{
    private const string Queued = "queued";
    private const string Processing = "processing";
    private const string Stopped = "stopped";
    private const string Succeeded = "succeeded";
    private const string None = "none";
    private const string Authority = "authority";
    private const string Target = "target";
    private const string Storage = "storage";
    private const string Clock = "clock";

    private const int RevisionOffset = 66;

    /// <summary>Checks application/job/key/version bindings independently of schema annotations before any browser publication.</summary>
    internal static GrantProxyResult Parse(byte[] bytes, GrantProxyRequest request, string? tag, string? location)
    {
        if (request.Mutation)
        {
            var accepted = ApplicationJson.Parse<GrantAcceptance>(bytes);
            if (accepted.RequestKey != request.Key || !Valid(accepted.Job, request) || accepted.Job.Version != tag ||
                location != request.Location(accepted.Job.Id))
            {
                throw new InvalidDataException();
            }
            return new(StatusCodes.Status202Accepted, accepted.Job);
        }
        if (request.JobId is not null)
        {
            var job = ApplicationJson.Parse<GrantJobView>(bytes);
            return Valid(job, request) && job.Version == tag ? new(StatusCodes.Status200OK, job) : throw new InvalidDataException();
        }
        var page = ApplicationJson.Parse<GrantJobPage>(bytes);
        if (page.Items is null || page.Items.Count > GrantRevocationRules.RecentLimit || page.ExpiresAt.Offset != TimeSpan.Zero ||
            page.Items.Any(job => !Valid(job, request) || job.ExpiresAt != page.ExpiresAt) || page.Items.Select(job => job.Id).Distinct(StringComparer.Ordinal).Count() != page.Items.Count)
        {
            throw new InvalidDataException();
        }
        return new(StatusCodes.Status200OK, Page: page);
    }

    /// <summary>Preserves lossless nonnegative Int64 counts and a closed state/failure vocabulary with exact target and job-version identity.</summary>
    private static bool Valid(GrantJobView? job, GrantProxyRequest request)
    {
        if (job is null || !GrantRevocationRules.TryKey(job.Id, out _) || job.ApplicationId != request.Id ||
            request.JobId is not null && job.Id != request.JobId || !ApplicationVersion.IsCanonical(job.Version) ||
            job.State is not (Queued or Processing or Stopped or Succeeded) || job.Failure is not (None or Authority or Target or Storage or Clock) ||
            (job.State == Stopped) != (job.Failure != None) || !Count(job.AuthorizationCount) || !Count(job.TokenCount) ||
            job.CreatedAt.Offset != TimeSpan.Zero || job.UpdatedAt.Offset != TimeSpan.Zero || job.ExpiresAt.Offset != TimeSpan.Zero ||
            job.CreatedAt.ToUnixTimeSeconds() < 0 || job.UpdatedAt < job.CreatedAt ||
            job.CreatedAt.Ticks % TimeSpan.TicksPerSecond != 0 || job.UpdatedAt.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            return false;
        }
        var revision = long.Parse(job.Version.AsSpan(RevisionOffset, job.Version.Length - RevisionOffset - 1), CultureInfo.InvariantCulture);
        return ApplicationVersion.Matches(job.Version, job.Id, revision);
    }

    /// <summary>Rejects signs, whitespace, leading zeroes, overflow and rounded number representations.</summary>
    private static bool Count(string? value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) &&
        count >= 0 && value == count.ToString(CultureInfo.InvariantCulture);
}
