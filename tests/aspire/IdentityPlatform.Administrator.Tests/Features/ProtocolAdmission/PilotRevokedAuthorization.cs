using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Creates a precise native authorization-status change while preserving the genuine protected code and all unrelated rows.</summary>
internal static class PilotRevokedAuthorization
{
    private const string UnexpectedEntry = "Rejected authorization created an unexpected native entry.";
    /// <summary>Uses native managers in a separate committed scope; no request cache or principal is edited.</summary>
    internal static async Task<string> RevokeAsync(BrowserHttpsFixture environment, string subject)
    {
        await using var scope = Observer(environment);
        var token = TestContext.Current.CancellationToken;
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var codes = new List<object>();
        await foreach (var candidate in manager.FindBySubjectAsync(subject, token))
        {
            if (await manager.HasTypeAsync(candidate, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode, token))
            {
                codes.Add(candidate);
            }
        }
        var code = Assert.Single(codes);
        Assert.True(await manager.HasStatusAsync(code, OpenIddictConstants.Statuses.Valid, token));
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.FindByIdAsync((await manager.GetAuthorizationIdAsync(code, token))!, token);
        Assert.NotNull(authorization);
        Assert.True(await authorizations.TryRevokeAsync(authorization, token));
        return (await manager.GetIdAsync(code, token))!;
    }

    /// <summary>Reads fresh persisted state after HTTP rejection; neither consumption nor a new winning entry is allowed.</summary>
    internal static async Task RequireUnconsumedAsync(BrowserHttpsFixture environment, string codeId)
    {
        await using var scope = Observer(environment);
        var token = TestContext.Current.CancellationToken;
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var code = await manager.FindByIdAsync(codeId, token);
        Assert.NotNull(code);
        Assert.True(await manager.HasStatusAsync(code, OpenIddictConstants.Statuses.Valid, token));
        var count = 0;
        await foreach (var entry in manager.FindByAuthorizationIdAsync((await manager.GetAuthorizationIdAsync(code, token))!, token))
        {
            Assert.True(string.Equals(codeId, await manager.GetIdAsync(entry, token), StringComparison.Ordinal), UnexpectedEntry);
            count++;
        }
        Assert.Equal(1, count);
    }

    /// <summary>Preserves explicit mixed-store selection without introducing selection services into standalone hosting.</summary>
    private static AsyncServiceScope Observer(BrowserHttpsFixture environment)
    {
        var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
        return scope;
    }
}
