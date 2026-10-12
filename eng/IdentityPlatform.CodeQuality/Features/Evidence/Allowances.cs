using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPlatform.CodeQuality.Features.Analysis;

namespace IdentityPlatform.CodeQuality.Features.Evidence;

/// <summary>Keep unchanged legacy debt separate from owned, expiring exceptions for justified new findings.</summary>
internal static class Allowances
{
    private const string EvidencePath = "eng/code-quality/allowances.json";
    private const string InvalidEvidence = "Invalid, duplicated, stale or expired code-quality allowance.";
    private const string UnresolvedFinding = "New or changed code-quality finding";
    private const string Accepted = "PASS: code-quality rules; unchanged legacy debt remains explicitly tracked.";
    private static readonly string[] RemediationTasks = ["T013", "T014", "T015", "T016"];
    private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>Reject unmatched findings and stale entries; this read-only operation never refreshes a baseline.</summary>
    internal static void Require(string root, IReadOnlyList<Finding> findings)
    {
        var evidence = JsonSerializer.Deserialize<Evidence>(File.ReadAllText(Path.Combine(root, EvidencePath)), JsonOptions)
            ?? throw new InvalidDataException(InvalidEvidence);
        Validate(evidence, findings, DateOnly.FromDateTime(DateTime.UtcNow));
        var allowed = evidence.Legacy.Select(entry => entry.Finding).Concat(evidence.Exceptions.Select(entry => entry.Finding)).ToHashSet();
        var newFindings = findings.Where(finding => !allowed.Contains(finding)).ToArray();
        foreach (var finding in newFindings)
        {
            Console.Error.WriteLine($"{UnresolvedFinding}: {finding.Rule} {finding.Path} {finding.Symbol}");
        }
        if (newFindings.Length > 0)
        {
            throw new InvalidDataException(UnresolvedFinding);
        }

        Console.WriteLine(Accepted);
    }

    /// <summary>Validate exact identities and metadata against current findings using an explicit clock for boundary tests.</summary>
    internal static void Validate(Evidence evidence, IReadOnlyList<Finding> findings, DateOnly today)
    {
        if (evidence.Legacy is null || evidence.Exceptions is null)
        {
            throw new InvalidDataException(InvalidEvidence);
        }

        var allowed = evidence.Legacy.Select(entry => entry.Finding).Concat(evidence.Exceptions.Select(entry => entry.Finding)).ToArray();
        if (allowed.Distinct().Count() != allowed.Length || allowed.Any(entry => !findings.Contains(entry)))
        {
            throw new InvalidDataException(InvalidEvidence);
        }

        if (evidence.Legacy.Any(entry => !RemediationTasks.Contains(entry.Task) || string.IsNullOrWhiteSpace(entry.Owner)))
        {
            throw new InvalidDataException(InvalidEvidence);
        }

        if (evidence.Exceptions.Any(entry => entry.Expires <= today || string.IsNullOrWhiteSpace(entry.Reason) ||
            string.IsNullOrWhiteSpace(entry.Owner) || !RemediationTasks.Contains(entry.Task)))
        {
            throw new InvalidDataException(InvalidEvidence);
        }
    }
}

/// <summary>Stores exact legacy findings and separately reviewed temporary exceptions.</summary>
internal sealed record Evidence(LegacyEntry[] Legacy, ExceptionEntry[] Exceptions);

/// <summary>Links a pre-existing finding to a separately reviewed cleanup task and accountable owner.</summary>
internal sealed record LegacyEntry(Finding Finding, string Task, string Owner);

/// <summary>Binds a justified exception to exact source, accountable review and an exclusive expiry date.</summary>
internal sealed record ExceptionEntry(Finding Finding, string Task, string Owner, string Reason, DateOnly Expires);
