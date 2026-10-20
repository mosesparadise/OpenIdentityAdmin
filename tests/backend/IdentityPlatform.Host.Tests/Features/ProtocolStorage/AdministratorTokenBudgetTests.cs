using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks independent exact ceilings, overflow denials and conservative retention boundaries for native artifact reservations.</summary>
public sealed class AdministratorTokenBudgetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private const int RetentionDays = 14;
    private const int GraceSeconds = 30;

    /// <summary>Every retained category counts at its own boundary, and each native creation must fit all affected budgets together.</summary>
    [Theory]
    [InlineData(19999, 1999, 9999, 127, 0, true)]
    [InlineData(20000, 1999, 9999, 127, 0, false)]
    [InlineData(19999, 2000, 9999, 127, 0, true)]
    [InlineData(19999, 1999, 10000, 127, 0, false)]
    [InlineData(19999, 1999, 9999, 128, 0, false)]
    [InlineData(19999, 1999, 10000, 128, 1, true)]
    [InlineData(19999, 2000, 10000, 128, 1, false)]
    [InlineData(20000, 1999, 10000, 128, 1, false)]
    [InlineData(19998, 2000, 10000, 128, 2, true)]
    [InlineData(19999, 2000, 10000, 128, 2, false)]
    [InlineData(0, 2001, 0, 0, 2, false)]
    [InlineData(0, 0, 10001, 0, 2, false)]
    [InlineData(0, 0, 0, 129, 2, false)]
    [InlineData(-1, 0, 0, 0, 0, false)]
    [InlineData(2147483647, 0, 0, 0, 2, false)]
    public void Given_NativeCounts_When_ReservingFixedCreation_Then_EveryHardLimitApplies(int tokens, int authorizations, int requests, int live, int creation, bool allowed)
    {
        var current = new AdministratorTokenCounts(tokens, authorizations, requests, live);
        Assert.Equal(allowed, current.CanAdd(AdministratorTokenCounts.For((AdministratorTokenCreation)creation)));
    }

    /// <summary>Uses additive native skew and rollback grace while keeping the explicit fourteen-day minimum creation age.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(300)]
    public void Given_NativeValidationSkew_When_DerivingCleanupCutoffs_Then_BothGraceWindowsAreRetained(int seconds)
    {
        Assert.True(AdministratorTokenBudget.TryCutoffs(Now, TimeSpan.FromSeconds(seconds), out var age, out var expiration));
        Assert.Equal(Now.UtcDateTime.AddDays(-RetentionDays), age);
        Assert.Equal(Now.UtcDateTime.AddSeconds(-seconds - GraceSeconds), expiration);
    }

    /// <summary>Malformed and unrepresentable policy never wraps a cleanup cutoff into a permissive date.</summary>
    [Fact]
    public void Given_InvalidOrOverflowingTime_When_DerivingCleanupCutoffs_Then_NoDeletionIsAuthorized()
    {
        Assert.False(AdministratorTokenBudget.TryCutoffs(Now, TimeSpan.FromTicks(-1), out _, out _));
        Assert.False(AdministratorTokenBudget.TryCutoffs(Now, TimeSpan.MaxValue, out _, out _));
        Assert.False(AdministratorTokenBudget.TryCutoffs(DateTimeOffset.MinValue, TimeSpan.Zero, out _, out _));
        Assert.Throws<InvalidOperationException>(() => AdministratorTokenCounts.For((AdministratorTokenCreation)int.MaxValue));
    }
}
