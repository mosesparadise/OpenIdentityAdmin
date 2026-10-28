using System.Text.Json.Nodes;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises exact cleanup receipt ownership before and after real native target rejection.</summary>
internal static class CleanupAssertionOwnership
{
    private const int ProofLifetime = 60;
    private const string Changed = "changed-cleanup-request-value";
    private const string ExtraParameter = "cleanup-binding-characterization";
    private const string FirstElement = "a";
    private const string SecondElement = "b";
    private const string ArrayText = "a, b";
    private const string BooleanText = "true";
    private const string NumberText = "1";
    private const string JsonText = "[\"a\"]";
    private const string NestedKey = "nested";
    private const string ValueKey = "value";
    private const string OriginalValue = "original";

    /// <summary>Runs independent fresh-proof controls for every retained ownership boundary and irreversible consumption fault.</summary>
    internal static async Task VerifyAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        foreach (var change in Enum.GetValues<Change>())
        {
            await ContinuationAsync(fixture, change, token);
        }
        await RejectedUnclaimedAsync(fixture, token);
        await RejectedClaimedAsync(fixture, token);
        await CleanupAssertionCommitFaults.VerifyAsync(fixture, token);
    }

    /// <summary>Creates a real early receipt and tests one fresh child epoch, never a fake assertion or an already disposed request.</summary>
    private static async Task ContinuationAsync(CleanupAssertionFixture fixture, Change change, CancellationToken token)
    {
        var proof = fixture.Proof();
        var probe = new CleanupAssertionProbe
        {
            BeforeDispatch = authentication => SeedTypedParameter(authentication, change),
            BeforeTarget = (authentication, _) =>
            new ValueTask(CheckAsync(fixture, authentication, change))
        };
        var operation = CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token);
        try
        {
            if (change is Change.ForeignTransaction or Change.DuplicateClaim or Change.StalePhase)
            {
                await Assert.ThrowsAnyAsync<InvalidOperationException>(() => operation);
            }
            else
            {
                var result = await operation;
                if (change is Change.None or Change.RegistrationRevision or Change.Application or Change.Scope or Change.ReplacedContext or
                    Change.RejectedContext or Change.HandledContext or Change.SkippedContext)
                {
                    Assert.Equal(OpenIddictConstants.Errors.InvalidToken, result.Error);
                }
            }
            Assert.Equal(1, probe.Reached);
            Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
            await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
        }
        finally { fixture.First.GetRequiredService<NativeConsumptionClock>().Set(fixture.Now); }
    }

    /// <summary>Claims once inside an actual fresh locked phase and verifies precise substitutions against the accepted positive control.</summary>
    private static Task<bool> CheckAsync(CleanupAssertionFixture fixture, OpenIddictServerEvents.ProcessAuthenticationContext authentication, Change change) =>
        ProtocolStorageChild.RunAsync(fixture.First.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
            await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
            var native = change == Change.ForeignTransaction
                ? await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync() : authentication.Transaction;
            if (change == Change.ForeignTransaction)
            {
                native.EndpointType = authentication.EndpointType;
                native.Request = authentication.Request;
                native.BaseUri = authentication.Transaction.BaseUri;
                native.RequestUri = authentication.Transaction.RequestUri;
            }
            var context = new OpenIddictServerEvents.HandleRevocationRequestContext(native);
            var receipt = Receipt(authentication);
            receipt.Claim(scope, context);
            if (change == Change.DuplicateClaim)
            {
                Assert.Throws<InvalidOperationException>(() => receipt.Claim(scope, context));
                Assert.Throws<InvalidOperationException>(() => ProtocolStorageAmbient.Current!.Request.RequireCurrent());
                Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture(write: true));
                return true;
            }
            var registration = await AdministratorRegistration.ReadCleanupLockedAsync(scope, fixture.Material.Policy, cancellation);
            Assert.NotNull(registration);
            Assert.True(receipt.IsCurrent(scope, context, registration, cancellation));
            registration = await ChangeRegistrationAsync(fixture, scope, registration, change, cancellation);
            ChangeRequest(fixture, authentication, change);
            if (change == Change.ReplacedContext)
            {
                context = new(native);
            }
            if (change == Change.RejectedContext)
            {
                context.Reject(OpenIddictConstants.Errors.InvalidRequest);
            }
            if (change == Change.HandledContext)
            {
                context.HandleRequest();
            }
            if (change == Change.SkippedContext)
            {
                context.SkipRequest();
            }
            if (change == Change.StalePhase)
            {
                await transaction.CommitAsync(cancellation);
            }
            Assert.Equal(change == Change.None, receipt.IsCurrent(scope, context, registration, cancellation));
            return true;
        }).AsTask();

    /// <summary>Changes one registration identity at a time without allowing application or scope equality to stand in for revision ownership.</summary>
    private static async Task<AdministratorCleanupRegistration> ChangeRegistrationAsync(CleanupAssertionFixture fixture,
        ProtocolStorageScope scope, AdministratorCleanupRegistration original, Change change, CancellationToken token)
    {
        if (change is not (Change.RegistrationRevision or Change.Application or Change.Scope))
        {
            return original;
        }
        string? replacement = null;
        if (change == Change.Application)
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
            await manager.DeleteAsync((await manager.FindByClientIdAsync(AdministratorProtocolIdentity.ClientId, token))!, token);
            replacement = await manager.GetIdAsync(await manager.CreateAsync(fixture.Material.Policy.Application(), token), token);
        }
        if (change == Change.Scope)
        {
            var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            await manager.DeleteAsync((await manager.FindByNameAsync(AdministratorProtocolIdentity.Scope, token))!, token);
            replacement = await manager.GetIdAsync(await manager.CreateAsync(AdministratorRegistrationPolicy.Scope(), token), token);
        }
        await scope.Owner.RunAsync(async cancellation =>
        {
            var profile = await scope.Owner.Database.Set<AdministratorProtocolProfile>().SingleAsync(cancellation);
            if (change == Change.RegistrationRevision)
            {
                profile.Revision++;
            }
            if (change == Change.Application)
            {
                profile.ApplicationId = replacement!;
            }
            if (change == Change.Scope)
            {
                profile.ScopeId = replacement!;
            }
            await scope.Owner.Database.SaveChangesAsync(cancellation);
        }, token, write: true);
        return Assert.IsType<AdministratorCleanupRegistration>(await AdministratorRegistration.ReadCleanupLockedAsync(scope, fixture.Material.Policy, token));
    }

    /// <summary>Mutates original native inputs only in explicit hostile component controls; ordinary rejection observers remain passive.</summary>
    private static void ChangeRequest(CleanupAssertionFixture fixture, OpenIddictServerEvents.ProcessAuthenticationContext authentication, Change change)
    {
        switch (change)
        {
            case Change.Expiry:
                fixture.First.GetRequiredService<NativeConsumptionClock>().Set(fixture.Now.AddSeconds(ProofLifetime));
                break;
            case Change.Proof:
                authentication.ClientAssertionPrincipal!.SetClaim(OpenIddictConstants.Claims.Subject, Changed);
                break;
            case Change.Endpoint:
                authentication.Transaction.EndpointType = OpenIddictServerEndpointType.Token;
                break;
            case Change.Request:
                authentication.Transaction.Request = new(authentication.Request.GetParameters());
                break;
            case Change.Parameters:
                authentication.Request.Token = Changed;
                break;
            case Change.StringArray:
                authentication.Request[ExtraParameter] = new OpenIddictParameter(System.Collections.Immutable.ImmutableArray.Create<string?>(FirstElement, SecondElement));
                break;
            case Change.StringBoolean:
                authentication.Request[ExtraParameter] = true;
                break;
            case Change.StringNumber:
                authentication.Request[ExtraParameter] = 1L;
                break;
            case Change.JsonString:
                authentication.Request[ExtraParameter] = new OpenIddictParameter(System.Collections.Immutable.ImmutableArray.Create<string?>(FirstElement));
                break;
            case Change.NestedNode:
                var node = (JsonObject)authentication.Request[ExtraParameter]!.Value.GetRawValue()!;
                node[NestedKey]![ValueKey] = Changed;
                authentication.Request[ExtraParameter] = node;
                break;
        }
    }

    /// <summary>Seeds pairs aliased by native ToString or JsonElement casts, preserving exact value kinds until the hostile substitution.</summary>
    private static void SeedTypedParameter(OpenIddictServerEvents.ProcessAuthenticationContext authentication, Change change)
    {
        switch (change)
        {
            case Change.StringArray:
                authentication.Request[ExtraParameter] = ArrayText;
                break;
            case Change.StringBoolean:
                authentication.Request[ExtraParameter] = BooleanText;
                break;
            case Change.StringNumber:
                authentication.Request[ExtraParameter] = NumberText;
                break;
            case Change.JsonString:
                authentication.Request[ExtraParameter] = JsonText;
                break;
            case Change.NestedNode:
                authentication.Request[ExtraParameter] = new JsonObject { [NestedKey] = new JsonObject { [ValueKey] = OriginalValue } };
                break;
        }
    }

    /// <summary>A still-unclaimed receipt is denied after actual native rejection while its original ambient request remains active.</summary>
    private static async Task RejectedUnclaimedAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var proof = fixture.Proof();
        var reached = 0;
        var probe = new CleanupAssertionProbe
        {
            AfterTarget = async (authentication, _) =>
        {
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, authentication.Error);
            reached++;
            await ProtocolStorageChild.RunAsync(fixture.First.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                Receipt(authentication).Claim(scope, new(authentication.Transaction));
                return true;
            });
        }
        };
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token));
        Assert.Equal(1, reached);
        Assert.Equal(1, probe.Reached);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
        await CleanupAssertionEligibility.DeniedAsync(fixture.Second, proof.Text, token);
    }

    /// <summary>Actual native target validation invalidates a claimed receipt while its genuinely fresh child epoch remains live.</summary>
    private static async Task RejectedClaimedAsync(CleanupAssertionFixture fixture, CancellationToken token)
    {
        var reached = 0;
        var probe = new CleanupAssertionProbe
        {
            BeforeTarget = async (authentication, _) =>
        {
            await ProtocolStorageChild.RunAsync(fixture.First.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
            {
                await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
                await AdministratorProtocolLocks.AcquireProfileAsync(scope.Owner, cancellation);
                var continuation = new OpenIddictServerEvents.HandleRevocationRequestContext(authentication.Transaction);
                var receipt = Receipt(authentication);
                receipt.Claim(scope, continuation);
                var binding = await AdministratorRegistration.ReadCleanupLockedAsync(scope, fixture.Material.Policy, cancellation);
                Assert.NotNull(binding);
                Assert.True(receipt.IsCurrent(scope, continuation, binding, cancellation));
                await scope.Services.GetRequiredService<OpenIddictServerHandlers.ValidateGenericToken>().HandleAsync(authentication);
                Assert.Equal(OpenIddictConstants.Errors.InvalidToken, authentication.Error);
                reached++;
                Assert.False(receipt.IsCurrent(scope, continuation, binding, cancellation));
                return true;
            });
        }
        };
        var proof = fixture.Proof();
        var result = await CleanupAssertionFixture.AuthenticateAsync(fixture.First, proof.Text, probe, token);
        Assert.Equal(OpenIddictConstants.Errors.InvalidToken, result.Error);
        Assert.Equal(1, reached);
        Assert.Equal(1, probe.Reached);
        Assert.True(await CleanupAssertionFixture.RetainedAsync(fixture.Second, proof.Digest, token));
    }

    /// <summary>Requires the actual typed result produced by the installed cleanup consumer.</summary>
    internal static DurableCleanupClientAssertion Receipt(OpenIddictServerEvents.ProcessAuthenticationContext context) =>
        Assert.IsType<DurableCleanupClientAssertion>(context.Transaction.GetProperty<DurableCleanupClientAssertion>(typeof(DurableCleanupClientAssertion).FullName!));

    /// <summary>Names independent substitutions and native continuation state changes in the finite component inventory.</summary>
    private enum Change
    {
        None, Expiry, Proof, Endpoint, Request, Parameters, RegistrationRevision, Application, Scope, ReplacedContext,
        RejectedContext, HandledContext, SkippedContext, ForeignTransaction, DuplicateClaim, StalePhase,
        StringArray, StringBoolean, StringNumber, JsonString, NestedNode
    }
}
