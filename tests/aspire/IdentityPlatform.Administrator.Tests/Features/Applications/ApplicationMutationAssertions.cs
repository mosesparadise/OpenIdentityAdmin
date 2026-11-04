using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Independently checks persisted native configuration and exact finite audits instead of trusting mutation response projections.</summary>
internal static class ApplicationMutationAssertions
{
    private static readonly string[] Permissions = ["ept:authorization", "ept:token", "gt:authorization_code", "rst:code"];
    private static readonly string[] Requirements = ["ft:pkce"];
    private const string Web = "web";
    private const string Confidential = "confidential";
    private const string Consent = "explicit";
    private const string Actor = "User";
    private const string Target = "Application";
    private const string Accepted = "Accepted";
    private static readonly string[] Events = ["ApplicationCreated", "ApplicationEdited"];

    /// <summary>Reads a fresh native descriptor, then checks actual audit identities without importing the candidate profile helper.</summary>
    internal static async Task RequireAsync(ApplicationMutationFixture test, ApplicationMutationResult result, string display, string redirect)
    {
        await using var scope = test.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var row = await manager.FindByIdAsync(result.Application!.Id, test.Token);
        Assert.NotNull(row);
        var native = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(native, row, test.Token);
        Assert.Equal(ApplicationMutationFixture.Client, native.ClientId);
        Assert.Equal(display, native.DisplayName);
        Assert.Equal(redirect, Assert.Single(native.RedirectUris).OriginalString);
        Assert.Equal(Web, native.ApplicationType);
        Assert.Equal(Confidential, native.ClientType);
        Assert.Equal(Consent, native.ConsentType);
        Assert.Equal(Permissions.Order(StringComparer.Ordinal), native.Permissions.Order(StringComparer.Ordinal));
        Assert.Equal(Requirements, native.Requirements);
        var audits = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>().AsNoTracking()
            .Where(value => value.TargetType == Target).ToArrayAsync(test.Token);
        Assert.Equal(Events.Order(StringComparer.Ordinal), audits.Select(value => value.Event).Order(StringComparer.Ordinal));
        Assert.All(audits, audit =>
        {
            Assert.Equal(Actor, audit.ActorType);
            Assert.Equal(test.Identity.Subject, audit.ActorId);
            Assert.Equal(result.Application.Id, audit.TargetId);
            Assert.Equal(Accepted, audit.Outcome);
            Assert.NotEqual(Guid.Empty, audit.CorrelationId);
            Assert.Null(audit.RelatedCorrelationId);
        });
    }
}
