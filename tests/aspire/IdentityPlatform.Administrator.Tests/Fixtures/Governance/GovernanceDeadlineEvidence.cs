namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Distinguishes managed cancellation from the independent provider deadline using callback-entry facts.</summary>
internal static class GovernanceDeadlineEvidence
{
    /// <summary>Accepts exact provider timeouts after the budget; cancellation shapes additionally require the original canceled token.</summary>
    internal static bool Accept(bool caller, bool elapsed, bool tokenCanceled, bool cancellationShape, bool providerTimeout)
    {
        RequireControls();
        return Matches(caller, elapsed, tokenCanceled, cancellationShape, providerTimeout);
    }

    /// <summary>Keeps caller, elapsed-budget, token and native-shape requirements independent.</summary>
    private static bool Matches(bool caller, bool elapsed, bool tokenCanceled, bool cancellationShape, bool providerTimeout) =>
        !caller && elapsed && (providerTimeout || tokenCanceled && cancellationShape);

    /// <summary>Rejects early, caller-induced, unrecognized and uncanceled cancellation results while accepting both independent deadlines.</summary>
    private static void RequireControls()
    {
        Assert.True(Matches(false, true, false, false, true));
        Assert.True(Matches(false, true, true, false, true));
        Assert.True(Matches(false, true, true, true, false));
        Assert.False(Matches(false, false, false, false, true));
        Assert.False(Matches(false, false, true, true, false));
        Assert.False(Matches(true, true, false, false, true));
        Assert.False(Matches(true, true, true, true, false));
        Assert.False(Matches(false, true, false, true, false));
        Assert.False(Matches(false, true, true, false, false));
        Assert.False(Matches(false, true, false, false, false));
        Assert.False(CeremonyCancellation.IsProviderTimeout(new OperationCanceledException(), true));
        Assert.False(CeremonyCancellation.IsProviderTimeout(new InvalidOperationException(), true));
        Assert.False(CeremonyCancellation.IsProviderTimeout(null, true));
    }
}
