using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Proves exact native proof, shared cross-endpoint replay and the deliberately narrow central-disablement exception.</summary>
internal static class CleanupAssertionEligibility
{
    private const int MaximumProofBytes = 8192;
    private const string WrongMethod = "wrong-cleanup-proof-method";
    private const string ForbiddenSecret = "synthetic-forbidden-client-secret";
    private const string ChangedFingerprint = "changed-cleanup-profile-key";

    /// <summary>Preserves native proof validation while observing durable receipt creation before the mandatory invalid-target rejection.</summary>
    internal static async Task VerifyAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        await CrossEndpointAsync(fixture, token);
        await ConcurrentAsync(fixture, token);
        await DisabledAsync(fixture, token);
        await InvalidProofsAsync(fixture, token);
        await RegistrationAsync(fixture, token);
    }

    /// <summary>Cleanup, PAR and token share one digest namespace across separately composed roots, including failed token-grant validation.</summary>
    private static async Task CrossEndpointAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var proof = fixture.Proof();
        await AcceptedAsync(fixture.First, proof.Text, token);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await DeniedAsync(fixture.Second, proof.Text, token);
        foreach (var tokenEndpoint in new[] { false, true })
        {
            var replay = await NativeAssertionAuthentication.AuthenticateAsync(fixture.Second, proof.Text, tokenEndpoint, token);
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, replay.Error);
            Assert.Null(replay.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!));
            var reverse = fixture.Proof();
            var original = await NativeAssertionAuthentication.AuthenticateAsync(fixture.First, reverse.Text, tokenEndpoint, token);
            Assert.Equal(tokenEndpoint ? OpenIddictConstants.Errors.InvalidToken : null, original.Error);
            Assert.NotNull(original.ClientAssertionPrincipal);
            Assert.NotNull(original.Transaction.GetProperty<DurableClientAssertion>(typeof(DurableClientAssertion).FullName!));
            Assert.Null(original.Transaction.GetProperty<DurableCleanupClientAssertion>(typeof(DurableCleanupClientAssertion).FullName!));
            await DeniedAsync(fixture.Second, reverse.Text, token);
            Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, reverse.Digest, token));
        }
    }

    /// <summary>Two real provider owners race the same cryptographic proof; exactly one consumes and only one observer is reached.</summary>
    private static async Task ConcurrentAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var count = await CleanupAssertionFixture.CountAsync(fixture.Second, token);
        var proof = fixture.Proof();
        var first = new CleanupAssertionProbe();
        var second = new CleanupAssertionProbe();
        var outcomes = await Task.WhenAll(CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, first, token),
            CleanupAssertionFixture.AuthenticateAsync(fixture.Second, proof.Text, second, token));
        Assert.Single(outcomes, outcome => outcome.Error == OpenIddictConstants.Errors.InvalidToken);
        Assert.Single(outcomes, outcome => outcome.Error == OpenIddictConstants.Errors.InvalidClient);
        Assert.Equal(1, first.Reached + second.Reached);
        Assert.Equal(count + 1, await CleanupAssertionFixture.CountAsync(fixture.Second, token));
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
    }

    /// <summary>Central disablement preserves deny-only client proof while both issuance endpoints still reject without consuming fresh proofs.</summary>
    private static async Task DisabledAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        await CleanupAssertionFixture.SetEnabledAsync(fixture.First, false, token);
        try
        {
            foreach (var tokenEndpoint in new[] { false, true })
            {
                var proof = fixture.Proof();
                var denied = await NativeAssertionAuthentication.AuthenticateAsync(fixture.First, proof.Text, tokenEndpoint, token);
                Assert.Equal(OpenIddictConstants.Errors.InvalidClient, denied.Error);
                Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
                await AcceptedAsync(fixture.Second, proof.Text, token);
                Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.First, proof.Digest, token));
            }
        }
        finally { await CleanupAssertionFixture.SetEnabledAsync(fixture.First, true, token); }
        var restored = await NativeAssertionAuthentication.AuthenticateAsync(fixture.Second, fixture.Proof().Text, false, token);
        Assert.False(restored.IsRejected);
    }

    /// <summary>Wrong key, malformed proof, wrong authentication method and client-secret substitution never create a cleanup receipt or row.</summary>
    private static async Task InvalidProofsAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var before = await CleanupAssertionFixture.CountAsync(fixture.Second, token);
        using var foreign = new NativeAssertionMaterial(browser: true);
        await DeniedAsync(fixture.First, foreign.Sign(fixture.Now.ToUnixTimeSeconds()), token);
        foreach (var malformed in new[] { string.Empty, new string('a', MaximumProofBytes + 1), new string('\u00e9', MaximumProofBytes) })
        {
            await DeniedAsync(fixture.First, malformed, token);
        }
        foreach (var method in new[] { true, false })
        {
            var probe = new CleanupAssertionProbe
            {
                BeforeDispatch = context =>
            {
                if (method)
                {
                    context.Request.ClientAssertionType = WrongMethod;
                }
                else
                {
                    context.Request.ClientSecret = ForbiddenSecret;
                }
            }
            };
            var denied = await CleanupAssertionFixture.AuthenticateAsync(fixture.First, fixture.Proof().Text, probe, token);
            Assert.Equal(OpenIddictConstants.Errors.InvalidClient, denied.Error);
            Assert.Equal(0, probe.Reached);
            Assert.Null(probe.Receipt);
        }
        Assert.Equal(before, await CleanupAssertionFixture.CountAsync(fixture.Second, token));
        await AcceptedAsync(fixture.First, fixture.Proof().Text, token);
    }

    /// <summary>Native enabled markers, permission sets and the persisted key identity stay exact even though central disablement is tolerated.</summary>
    private static async Task RegistrationAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        foreach (var change in Enum.GetValues<RegistrationChange>())
        {
            var proof = fixture.Proof();
            await ChangeRegistrationAsync(fixture, change, restore: false, token);
            try
            {
                await DeniedAsync(fixture.First, proof.Text, token);
                Assert.False(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
            }
            finally { await ChangeRegistrationAsync(fixture, change, restore: true, token); }
            await AcceptedAsync(fixture.Second, proof.Text, token);
            Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.First, proof.Digest, token));
        }
    }

    /// <summary>Uses native manager updates for application/scope drift and changes only the explicit profile field under the same lock.</summary>
    private static Task<bool> ChangeRegistrationAsync(CleanupAssertionFixture fixture, RegistrationChange change, bool restore, CancellationToken token) =>
        CleanupAssertionFixture.MutateAsync(fixture.First, async (scope, current) =>
        {
            if (change == RegistrationChange.Fingerprint)
            {
                await scope.Owner.RunAsync(async cancellation =>
                {
                    var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(cancellation);
                    profile.PublicKeyFingerprint = restore ? fixture.Material.Policy.Key.Fingerprint : ChangedFingerprint;
                    await scope.Owner.Database.SaveChangesAsync(cancellation);
                }, current, write: true);
                return;
            }
            if (change == RegistrationChange.ScopeMarker)
            {
                var scopes = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
                var descriptor = AdministratorRegistrationPolicy.Scope();
                descriptor.Properties[AdministratorRegistrationPolicy.EnabledProperty] = JsonSerializer.SerializeToElement(restore);
                await scopes.UpdateAsync((await scopes.FindByNameAsync(AdministratorProtocolIdentity.Scope, current))!, descriptor, current);
                return;
            }
            var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            var application = fixture.Material.Policy.Application();
            if (!restore && change == RegistrationChange.ApplicationMarker)
            {
                application.Properties[AdministratorRegistrationPolicy.EnabledProperty] = JsonSerializer.SerializeToElement(false);
            }
            if (!restore && change == RegistrationChange.Permission)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
            }
            await applications.UpdateAsync((await applications.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, current))!, application, current);
        }, token);

    /// <summary>Requires the early cleanup-only receipt and final native target rejection without calling it a successful revocation.</summary>
    internal static async Task AcceptedAsync(IServiceProvider root, string proof, CancellationToken token)
    {
        var probe = new CleanupAssertionProbe();
        var result = await CleanupAssertionFixture.AuthenticateAsync(root, proof, probe, token);
        Assert.Equal(OpenIddictConstants.Errors.InvalidToken, result.Error);
        Assert.Equal(1, probe.Reached);
        Assert.NotNull(probe.Receipt);
    }

    /// <summary>Requires client-proof rejection before the observer and absence of any cleanup receipt.</summary>
    internal static async Task DeniedAsync(IServiceProvider root, string proof, CancellationToken token)
    {
        var probe = new CleanupAssertionProbe();
        var result = await CleanupAssertionFixture.AuthenticateAsync(root, proof, probe, token);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, result.Error);
        Assert.Equal(0, probe.Reached);
        Assert.Null(result.Transaction.GetProperty<DurableCleanupClientAssertion>(typeof(DurableCleanupClientAssertion).FullName!));
    }

    /// <summary>Names the distinct noncentral registration fields that cleanup is forbidden to relax.</summary>
    private enum RegistrationChange
    {
        ApplicationMarker, ScopeMarker, Permission, Fingerprint
    }
}
