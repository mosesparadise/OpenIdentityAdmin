namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Calibrates exact original-owner clearing, epoch replacement and sticky invalidation without a browser or private protocol payload.</summary>
internal static class ApplicationDocumentControls
{
    private const string Frame = "original-frame";
    private const string Original = "original-unique";
    private const string Replacement = "replacement-unique";
    private const string Other = "unrelated";
    private const int OriginalId = 1;
    private const int ReplacementId = 2;

    /// <summary>Requires explicit native clearing plus a distinct current owner and rejects missing, wrong, restored, disconnected and pre-arm evidence.</summary>
    internal static void Require()
    {
        foreach (var wholeEpoch in new[] { false, true })
        {
            var valid = Armed();
            valid.Cleared(ReplacementId, Other);
            valid.Cleared(OriginalId, Other);
            Assert.False(valid.Destroyed.IsCompleted);
            if (wholeEpoch)
            {
                valid.Cleared();
            }
            else
            {
                valid.Cleared(OriginalId, Original);
            }
            Assert.False(valid.IsDestroyed || valid.Destroyed.IsCompleted);
            valid.Created(Frame, ReplacementId, Replacement, true);
            Assert.True(valid.IsDestroyed && valid.Destroyed.IsCompletedSuccessfully);
            Assert.Equal(wholeEpoch ? ApplicationDocumentEnd.ContextsCleared : ApplicationDocumentEnd.IndividualDestroyed, valid.EndKind);
            Assert.True(valid.Seal());
            Assert.False(valid.Seal());
        }
        var missing = Armed();
        missing.Created(Frame, ReplacementId, Replacement, true);
        Assert.False(missing.IsDestroyed || missing.Destroyed.IsCompleted);
        var restored = Cleared();
        restored.Created(Frame, OriginalId, Original, true);
        restored.Created(Frame, ReplacementId, Replacement, true);
        Assert.False(restored.IsDestroyed || restored.Seal());
        var closed = Cleared();
        closed.Invalidate();
        Assert.False(closed.IsDestroyed || closed.Seal());
        var vanished = Cleared();
        vanished.Cleared(ReplacementId, Replacement);
        Assert.False(vanished.IsDestroyed || vanished.Seal());
        RequireBeforeArm();
    }

    /// <summary>Unrelated frames/default worlds cannot capture the owner, and clearing before arming invalidates that captured owner.</summary>
    private static void RequireBeforeArm()
    {
        var value = new ApplicationDocumentLedger(Frame);
        value.Created(Other, OriginalId, Original, true);
        value.Created(Frame, OriginalId, Original, false);
        Assert.False(value.Ready.IsCompleted);
        value.Created(Frame, OriginalId, Original, true);
        value.Cleared();
        Assert.Throws<Xunit.Sdk.TrueException>(value.Arm);
        var malformed = new ApplicationDocumentLedger(Frame);
        malformed.Created(Frame, OriginalId, string.Empty, true);
        Assert.Throws<Xunit.Sdk.TrueException>(malformed.Arm);
    }

    /// <summary>Creates one independently fixed original main/default owner before each control.</summary>
    private static ApplicationDocumentLedger Armed()
    {
        var value = new ApplicationDocumentLedger(Frame);
        value.Created(Frame, OriginalId, Original, true);
        value.Arm();
        value.RequireLive();
        return value;
    }

    /// <summary>Creates a completed whole-epoch transition for late invalidation controls.</summary>
    private static ApplicationDocumentLedger Cleared()
    {
        var value = Armed();
        value.Cleared();
        value.Created(Frame, ReplacementId, Replacement, true);
        Assert.True(value.IsDestroyed);
        return value;
    }
}
