using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Distinguishes native rejection before any write from an error after committed state consumption.</summary>
internal static class NativeClientDenial
{
    private const string RedeemedState = "InvalidToken/ID2139";

    /// <summary>Early replay has no committed phase or private error permit, so the production boundary suppresses all staged native output.</summary>
    internal static void RequireReplay(NativeClientHttpResult result)
    {
        RequireSuppressed(result);
        Assert.Equal(RedeemedState, result.Rejection);
    }

    /// <summary>Requires fail-closed publication without accepting arbitrary status codes or exposing unverified native response material.</summary>
    internal static void RequireSuppressed(NativeClientHttpResult result)
    {
        NativeClientInteroperabilityTests.RequireClean(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.Status);
        Assert.False(result.Authentication?.Succeeded == true);
        Assert.Equal(0, result.BodyLength);
        Assert.True(string.IsNullOrEmpty(result.Location));
        Assert.Empty(result.Cookies);
    }
}
