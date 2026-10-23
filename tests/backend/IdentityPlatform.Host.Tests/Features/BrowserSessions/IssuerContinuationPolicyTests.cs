using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks original-deadline and monotonic-clock boundaries without granting authority from representation.</summary>
public sealed class IssuerContinuationPolicyTests
{
    private const string Padding = "=";
    private const string Completed = "completed";
    private const string OtherClient = "other-client";
    private const string Interaction = "synthetic-interaction";
    private const string User = "synthetic-user";
    private const string Request = "synthetic-request";
    private const string BrowserDigest = "72DBB7336C76780023F83DA4C355F2EEEA85733B13D3477697917790C1229084";
    private const long Start = 1000;
    private const long Expiry = 1300;
    private const long Retention = 1330;
    private const long Observation = 1100;
    private const string Client = "identity-admin-bff";
    private const string Pending = "pending";

    /// <summary>Independent before/equality/after and rollback vectors preserve the original strict deadline without mutating final checks.</summary>
    [Theory]
    [InlineData(1069, false)]
    [InlineData(1070, true)]
    [InlineData(1099, true)]
    [InlineData(1100, true)]
    [InlineData(1299, true)]
    [InlineData(1300, false)]
    [InlineData(1301, false)]
    public void Given_OriginalContinuation_When_TimeIsChecked_Then_StrictDeadlineAndRollbackToleranceApplyWithoutMutation(long now, bool expected)
    {
        var row = Row();
        row.ObservedAt = Observation;
        Assert.Equal(expected, IssuerContinuationPolicy.IsCurrent(row, now));
        Assert.Equal(Observation, row.ObservedAt);
        Assert.Equal(Expiry, row.ExpiresAt);
        Assert.Equal(Retention, row.RetainUntil);
        Assert.Equal(expected, IssuerContinuationPolicy.Observe(row, now));
        Assert.Equal(expected ? Math.Max(now, Observation) : Observation, row.ObservedAt);
    }

    /// <summary>Canonical entry proofs must be different, remain digest-only, and cannot use padded or alias encodings.</summary>
    [Fact]
    public void Given_EntrySelectors_When_Parsed_Then_OnlyDistinctCanonicalProofsProduceDigestOnlyRequests()
    {
        var request = IssuerContinuationRequest.Read(BrowserLoginBindingTests.Intent, BrowserLoginBindingTests.Completion);
        Assert.NotNull(request);
        Assert.Equal(BrowserLoginBindingTests.Digest, request.IntentDigest);
        Assert.NotEqual(request.IntentDigest, request.BrowserDigest);
        Assert.Null(IssuerContinuationRequest.Read(BrowserLoginBindingTests.Intent, BrowserLoginBindingTests.Intent));
        Assert.Null(IssuerContinuationRequest.Read(BrowserLoginBindingTests.Intent + Padding, BrowserLoginBindingTests.Completion));
        Assert.Null(IssuerContinuationRequest.Read(string.Empty, BrowserLoginBindingTests.Completion));
    }

    /// <summary>Anonymous commit must reject pre-populated authority, altered registration, invalid digests or extended deadlines.</summary>
    [Theory]
    [InlineData(StartFault.State)]
    [InlineData(StartFault.Revision)]
    [InlineData(StartFault.Client)]
    [InlineData(StartFault.Correlation)]
    [InlineData(StartFault.Digest)]
    [InlineData(StartFault.SameProof)]
    [InlineData(StartFault.Interaction)]
    [InlineData(StartFault.Account)]
    [InlineData(StartFault.Session)]
    [InlineData(StartFault.Completion)]
    [InlineData(StartFault.Request)]
    [InlineData(StartFault.Expiry)]
    [InlineData(StartFault.Retention)]
    public void Given_AnonymousRow_When_AuthorityOrBoundsAreSubstituted_Then_FreshEntryValidationRejects(StartFault fault)
    {
        var row = Row();
        Assert.True(IssuerContinuationPolicy.IsStart(row));
        Alter(row, fault);
        Assert.False(IssuerContinuationPolicy.IsStart(row));
    }

    /// <summary>Substitutes exactly one anonymous-entry contract field while retaining its independent valid control.</summary>
    private static void Alter(IssuerContinuation row, StartFault fault)
    {
        Action change = fault switch
        {
            StartFault.State => () => row.State = Completed,
            StartFault.Revision => () => row.Revision++,
            StartFault.Client => () => row.ClientId = OtherClient,
            StartFault.Correlation => () => row.CorrelationId = Guid.Empty,
            StartFault.Digest => () => row.IntentDigest = row.IntentDigest.ToLowerInvariant(),
            StartFault.SameProof => () => row.BrowserDigest = row.IntentDigest,
            StartFault.Interaction => () => row.InteractionId = Interaction,
            StartFault.Account => () => row.UserId = User,
            StartFault.Session => () => row.SessionExpiresAt = Expiry,
            StartFault.Completion => () => row.CompletionDigest = row.IntentDigest,
            StartFault.Request => () => row.RequestId = Request,
            StartFault.Expiry => () => row.ExpiresAt++,
            StartFault.Retention => () => row.RetainUntil++,
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        change();
    }

    /// <summary>Builds independent contract values for policy checks; this row never acts as an authentication proof.</summary>
    private static IssuerContinuation Row() => new()
    {
        IntentDigest = BrowserLoginBindingTests.Digest,
        BrowserDigest = BrowserDigest,
        State = Pending,
        ClientId = Client,
        CreatedAt = Start,
        ExpiresAt = Expiry,
        RetainUntil = Retention,
        ObservedAt = Start,
        Revision = 1,
        CorrelationId = Guid.NewGuid()
    };
}

/// <summary>Separates anonymous row substitution controls by contract field.</summary>
public enum StartFault
{
    State, Revision, Client, Correlation, Digest, SameProof, Interaction, Account, Session, Completion, Request, Expiry, Retention
}
