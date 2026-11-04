using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Checks the pre-redemption input stamp without treating synthetic claims as real protocol authentication.</summary>
public sealed class ManagedConsumptionTests
{
    private const string Subject = "synthetic-subject";
    private const string Foreign = "foreign";

    /// <summary>The retained original input detects replacement, mutation and terminal outcomes across the child boundary.</summary>
    [Theory]
    [InlineData(ManagedInputDefect.Valid)]
    [InlineData(ManagedInputDefect.Request)]
    [InlineData(ManagedInputDefect.Parameter)]
    [InlineData(ManagedInputDefect.Authentication)]
    [InlineData(ManagedInputDefect.Principal)]
    [InlineData(ManagedInputDefect.Claims)]
    [InlineData(ManagedInputDefect.Endpoint)]
    [InlineData(ManagedInputDefect.Rejected)]
    [InlineData(ManagedInputDefect.Handled)]
    [InlineData(ManagedInputDefect.Skipped)]
    public async Task Given_CapturedManagedRedemptionInput_When_OriginalContextChanges_Then_OnlyUnchangedSuccessfulInputRemainsCurrent(ManagedInputDefect defect)
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        var current = await request.RunAsync(probe.Factory, (_, _) =>
        {
            var transaction = new OpenIddictServerTransaction { EndpointType = OpenIddictServerEndpointType.Token, Request = new() };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(Foreign)).SetClaim(OpenIddictConstants.Claims.Subject, Subject);
            var authentication = new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { AuthorizationCodePrincipal = principal };
            transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!, authentication);
            var signIn = new OpenIddictServerEvents.ProcessSignInContext(transaction) { Principal = principal };
            var input = new ManagedConsumptionInput(signIn);
            Mutate(defect, transaction, authentication, principal);
            return ValueTask.FromResult(input.Current());
        });
        Assert.Equal(defect == ManagedInputDefect.Valid, current);
    }

    /// <summary>Changes one original input after capture without calling any authentication or persistence shortcut.</summary>
    private static void Mutate(ManagedInputDefect defect, OpenIddictServerTransaction transaction,
        OpenIddictServerEvents.ProcessAuthenticationContext authentication, ClaimsPrincipal principal)
    {
        switch (defect)
        {
            case ManagedInputDefect.Request:
                transaction.Request = new();
                break;
            case ManagedInputDefect.Parameter:
                transaction.Request!.Code = Foreign;
                break;
            case ManagedInputDefect.Authentication:
                transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!, new OpenIddictServerEvents.ProcessAuthenticationContext(transaction) { AuthorizationCodePrincipal = principal });
                break;
            case ManagedInputDefect.Principal:
                authentication.AuthorizationCodePrincipal = principal.Clone();
                break;
            case ManagedInputDefect.Claims:
                principal.SetClaim(OpenIddictConstants.Claims.Subject, Foreign);
                break;
            case ManagedInputDefect.Endpoint:
                transaction.EndpointType = OpenIddictServerEndpointType.Authorization;
                break;
            case ManagedInputDefect.Rejected:
                authentication.Reject(OpenIddictConstants.Errors.InvalidGrant);
                break;
            case ManagedInputDefect.Handled:
                authentication.HandleRequest();
                break;
            case ManagedInputDefect.Skipped:
                authentication.SkipRequest();
                break;
        }
    }
}

/// <summary>Names each original input identity, value and outcome that must remain frozen.</summary>
public enum ManagedInputDefect
{
    Valid, Request, Parameter, Authentication, Principal, Claims, Endpoint, Rejected, Handled, Skipped
}
