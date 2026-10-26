using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Reads actual persisted original deadlines at distinct real HTTPS stages, without changing any stored lifetime.</summary>
internal static class BrowserLoginDeadline
{
    /// <summary>Moves only the fixture clock to the exact original product-intent boundary before resume dispatch.</summary>
    internal static async Task IntentAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var deadline = await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<BrowserLoginIntent>().AsNoTracking()
            .Select(row => row.ExpiresAt).SingleAsync(TestContext.Current.CancellationToken);
        fixture.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(deadline, TimeSpan.Zero);
    }

    /// <summary>Resolves the actual native PAR reference from the held authorization GET and uses its stored expiration equality.</summary>
    internal static async Task ParAsync(BrowserHttpsFixture fixture, Uri authorization)
    {
        var reference = QueryHelpers.ParseQuery(authorization.Query)[OpenIddictConstants.Parameters.RequestUri].ToString();
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(fixture.Issuer, reference);
        await using var scope = fixture.Issuer.Account.Journey.CreateScope();
        var expires = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Id == identifier).Select(row => row.ExpirationDate).SingleAsync(TestContext.Current.CancellationToken);
        Set(fixture, expires);
    }

    /// <summary>Reads the actual client state token after real issuer code issuance and before callback dispatch.</summary>
    internal static async Task StateAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var expires = await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Status == OpenIddictConstants.Statuses.Valid).Select(row => row.ExpirationDate).SingleAsync(TestContext.Current.CancellationToken);
        Set(fixture, expires);
    }

    /// <summary>Preserves the native stored UTC boundary exactly; no retry or deadline extension is introduced.</summary>
    private static void Set(BrowserHttpsFixture fixture, DateTime? expires)
    {
        Assert.NotNull(expires);
        fixture.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc));
    }
}
