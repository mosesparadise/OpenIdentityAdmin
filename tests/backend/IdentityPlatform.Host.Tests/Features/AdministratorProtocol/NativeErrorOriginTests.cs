using IdentityPlatform.AuthServer.Features.Protocol.Denials;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Seeds invalid origin transitions independently of native formatting so rejection alone cannot manufacture error authority.</summary>
public sealed class NativeErrorOriginTests
{
    private const string ChangedState = "changed-origin-state";

    /// <summary>The exact returned native rejection supports one normalized error, including native's omitted-error fallback.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ReturnedNativeRejection_When_ExactErrorClaimsOrigin_Then_SourceRemainsBound(bool omitted)
    {
        await using var probe = new AdministratorPassthroughProbe(true);
        await probe.RunAsync(async current =>
        {
            await NativeErrorOrigin.TrackAsync(current.Outer, () =>
            {
                current.Outer.Reject(omitted ? null : OpenIddictConstants.Errors.InvalidRequest);
                return ValueTask.CompletedTask;
            });
            var source = NativeErrorOrigin.Find(current.Outer.Transaction)!;
            var error = Error(current.Outer.Transaction);
            source.Claim(error);
            error.HandleRequest();
            source.RequireCompleted(error);
            Assert.Equal("The request could not be completed.", error.ErrorDescription);
            Assert.Null(error.ErrorUri);
        });
    }

    /// <summary>Each seeded defect must invalidate the whole original request, even when it otherwise carries a legitimate error name.</summary>
    [Theory]
    [InlineData(Change.BeforeReturn)]
    [InlineData(Change.Repeated)]
    [InlineData(Change.Foreign)]
    [InlineData(Change.Request)]
    [InlineData(Change.Parameters)]
    [InlineData(Change.SuccessfulIssuance)]
    [InlineData(Change.PendingIssuance)]
    [InlineData(Change.UnknownPhase)]
    public async Task Given_InvalidErrorOrigin_When_ErrorClaimsPublication_Then_RequestFailsClosed(Change change)
    {
        await using var probe = new AdministratorPassthroughProbe(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(async current =>
        {
            await NativeErrorOrigin.TrackAsync(current.Outer, () =>
            {
                var source = NativeErrorOrigin.Find(current.Outer.Transaction)!;
                current.Outer.Reject(OpenIddictConstants.Errors.InvalidRequest);
                if (change == Change.BeforeReturn)
                {
                    source.Claim(Error(current.Outer.Transaction));
                }
                if (change is Change.SuccessfulIssuance or Change.PendingIssuance)
                {
                    source.BeginIssuance(current.SignIn);
                    if (change == Change.SuccessfulIssuance)
                    {
                        current.SignIn.HandleRequest();
                        source.EndIssuance(current.SignIn);
                    }
                    else
                    {
                        current.SignIn.Reject(OpenIddictConstants.Errors.InvalidRequest);
                    }
                }
                return ValueTask.CompletedTask;
            });
            ClaimCorrupted(current, change);
        }));
    }

    /// <summary>Mutates only the selected origin invariant after real source return; it neither opens a database nor fabricates a publication permit.</summary>
    private static void ClaimCorrupted(AdministratorPassthroughProbe current, Change change)
    {
        var source = NativeErrorOrigin.Find(current.Outer.Transaction)!;
        var error = Error(current.Outer.Transaction);
        if (change == Change.Repeated)
        {
            source.Claim(error);
        }
        if (change == Change.Request)
        {
            current.Outer.Transaction.Request = new(current.Outer.Transaction.Request!.GetParameters());
        }
        if (change == Change.Parameters)
        {
            current.Outer.Transaction.Request!.State = ChangedState;
        }
        if (change == Change.Foreign)
        {
            error = Error(new());
        }
        if (change == Change.UnknownPhase)
        {
            var request = ProtocolStorageAmbient.Current!.Request;
            using var lifetime = new ProtocolStorageLifetime(request.Clock, ProtocolStorageLifetime.TransactionLimit, request.Token);
            request.Track(new ProtocolStoragePhase(lifetime));
        }
        source.Claim(error);
    }

    /// <summary>Creates a native-shaped notification for these focused provenance controls; HTTP formatter acceptance is tested separately.</summary>
    private static OpenIddictServerEvents.ProcessErrorContext Error(OpenIddictServerTransaction transaction) => new(transaction)
    {
        Error = OpenIddictConstants.Errors.InvalidRequest,
        Response = new()
    };

    /// <summary>Names one independent terminal/source invariant per permanent negative control.</summary>
    public enum Change
    {
        BeforeReturn, Repeated, Foreign, Request, Parameters, SuccessfulIssuance, PendingIssuance, UnknownPhase
    }
}
