using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Factors;

/// <summary>Checks native staged-verification rejection and narrowly scoped adapter configuration.</summary>
[Collection("administrator-bootstrap")]
public sealed class VerificationTests(BootstrapFixture fixture)
{
    private const string InvalidCode = "invalid";
    private const string FuturePurpose = "unsupported";
    private const string OtherStamp = "other-stamp";
    private const string UnsafeIssuer = "http://issuer.example.test/";
    private const string Owner = "owner";
    private const string Material = "synthetic";
    private const string Interaction = "interaction";

    /// <summary>Invalid syntax, wrong digits, expiry, unsupported purpose and stale stamp never yield an installation proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_StagedInteraction_When_NativeProofIsInvalid_Then_InstallationRemainsUnavailable(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        Assert.Null(await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System, interaction.Id, InvalidCode, state.Token));
        var invalidDigits = FindInvalidDigits();
        Assert.Null(await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System, interaction.Id, invalidDigits, state.Token));
        var valid = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        interaction.SecurityStamp = OtherStamp;
        await state.Database.SaveChangesAsync(state.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System, interaction.Id, valid, state.Token));
        interaction.SecurityStamp = state.User.SecurityStamp!;
        interaction.Purpose = FuturePurpose;
        await state.Database.SaveChangesAsync(state.Token);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, TimeProvider.System, interaction.Id, valid, state.Token));
        Assert.Null(state.Access.Staged);
        await transaction.RollbackAsync(state.Token);
    }

    /// <summary>Rejects verification and later installation exactly at product expiry while retaining native TOTP validation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_StagedProof_When_ClockReachesExpiry_Then_VerificationAndInstallationAreDenied(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var state = new FactorScenario(services);
        await state.InitializeAsync(lease);
        var interaction = await state.StageAsync();
        await using var transaction = await state.Database.Database.BeginTransactionAsync(state.Token);
        var clock = new FactorClock(DateTimeOffset.FromUnixTimeSeconds(interaction.ExpiresAt));
        var code = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await Assert.ThrowsAsync<FactorUnavailableException>(() => VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, clock, interaction.Id, code, state.Token));
        clock.Now = DateTimeOffset.FromUnixTimeSeconds(interaction.ExpiresAt - 1);
        var proof = await VerifiedStagedFactor.VerifyAsync(state.Database, state.Access, state.Users, clock, interaction.Id, code, state.Token);
        Assert.NotNull(proof);
        clock.Now = DateTimeOffset.FromUnixTimeSeconds(interaction.ExpiresAt);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => Assert.IsType<AdministratorUserManager>(state.Users).InstallAsync(proof, state.Token));
        Assert.Empty(await state.Database.Set<AdministratorFactor>().ToArrayAsync(state.Token));
    }

    /// <summary>Selects a deterministic nonmatching code outside every native clock-window candidate, without retrying authentication.</summary>
    private static string FindInvalidDigits()
    {
        const int WindowSteps = 3;
        const int StepSeconds = 30;
        const int CodeSpace = 1000000;
        const string Format = "D6";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var accepted = Enumerable.Range(-WindowSteps, WindowSteps + WindowSteps + 1)
            .Select(step => FactorOracle.Code(now + step * StepSeconds)).ToHashSet(StringComparer.Ordinal);
        return Enumerable.Range(0, CodeSpace).Select(value => value.ToString(Format, System.Globalization.CultureInfo.InvariantCulture))
            .First(value => !accepted.Contains(value));
    }

    /// <summary>Configuration and cryptographic purposes fail closed for invalid issuer, owner, generation and material kinds.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_InvalidProtectionConfiguration_When_AdapterIsUsed_Then_NoFallbackIsCreated(string provider)
    {
        Assert.Throws<FactorUnavailableException>(() => new ServiceCollection().AddAdministratorFactors(new Uri(UnsafeIssuer)));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddAdministratorFactors(null!));
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var scope = services.Services.CreateAsyncScope();
        var protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
        Assert.Throws<FactorUnavailableException>(() => protection.Protect(Material, "", 1, FactorProtection.Active));
        Assert.Throws<FactorUnavailableException>(() => protection.Protect(Material, Owner, 0, FactorProtection.Active));
        Assert.Throws<FactorUnavailableException>(() => protection.Protect(Material, Owner, 1, FuturePurpose));
        Assert.Throws<FactorUnavailableException>(() => protection.Protect(Material, Owner, 1, FactorProtection.Staged));
        Assert.Throws<FactorUnavailableException>(() => protection.Protect(Material, Owner, 1, FactorProtection.Replay, Interaction));
        var replay = protection.Protect(Material, Owner, 1, FactorProtection.Replay);
        Assert.True(protection.Unprotect(replay, Owner, 1, FactorProtection.Replay) == Material);
        Assert.Throws<FactorUnavailableException>(() => RecoveryVerifier.Hash("", 1, new string('A', CodeLength)));
        Assert.Throws<FactorUnavailableException>(() => RecoveryVerifier.Hash(Owner, 0, new string('A', CodeLength)));
        Assert.Throws<FactorUnavailableException>(() => RecoveryVerifier.Hash(Owner, 1, Material));
    }
    private const int CodeLength = 32;
}
