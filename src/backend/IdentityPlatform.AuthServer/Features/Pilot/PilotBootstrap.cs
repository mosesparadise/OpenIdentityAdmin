using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Pilot;

public sealed class PilotBootstrap(PlatformDbContext database, UserManager<PlatformUser> users,
    IOpenIddictApplicationManager applications, IOpenIddictScopeManager scopes,
    PilotEligibility eligibility, IConfiguration configuration, TimeProvider time)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var username = configuration["Pilot:UserName"];
        var password = configuration["Pilot:Password"];
        var secret = configuration["Pilot:ClientSecret"];
        var redirect = configuration["Pilot:RedirectUri"];
        if (username is not { Length: >= 1 and <= 128 } || password is not { Length: >= 16 and <= 256 } ||
            secret is not { Length: >= 32 and <= 256 } || redirect is not { Length: <= 512 } ||
            !Uri.TryCreate(redirect, UriKind.Absolute, out var callback) || callback.Scheme != "https" ||
            callback.UserInfo.Length != 0 || callback.Fragment.Length != 0 || callback.Query.Length != 0 ||
            !string.Equals(callback.AbsoluteUri, redirect, StringComparison.Ordinal))
        {
            throw new PilotConfigurationException();
        }
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var manifest = await database.Set<PilotManifest>().SingleOrDefaultAsync(value => value.Id == PilotProfile.Id, cancellationToken);
        var user = await users.FindByNameAsync(username);
        var application = await applications.FindByClientIdAsync(PilotProfile.ClientId, cancellationToken);
        var scope = await scopes.FindByNameAsync(PilotProfile.Scope, cancellationToken);
        if (manifest is not null)
        {
            if (user is null || application is null || scope is null || user.Id != manifest.UserId ||
                username != manifest.UserName || user.UserName != username || redirect != manifest.RedirectUri ||
                !await eligibility.UserAsync(user, cancellationToken) ||
                !await eligibility.RegistrationAsync(PilotProfile.ClientId, [OpenIddictConstants.Scopes.OpenId, PilotProfile.Scope], cancellationToken) ||
                user.PasswordHash is null || users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed ||
                !await applications.ValidateClientSecretAsync(application, secret, cancellationToken))
            {
                throw new PilotConfigurationException();
            }
            LoginAudit.Add(database, "pilot-initialization", "verified-no-op", time.GetUtcNow().ToUnixTimeSeconds(), user.Id, "local-operator");
        }
        else
        {
            if (user is not null || application is not null || scope is not null)
            {
                throw new PilotConfigurationException();
            }
            user = new PlatformUser { UserName = username, LockoutEnabled = true };
            if (!(await users.CreateAsync(user, password)).Succeeded)
            {
                throw new PilotConfigurationException();
            }
            var applicationDescriptor = new OpenIddictApplicationDescriptor
            {
                ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
                ClientId = PilotProfile.ClientId,
                ClientSecret = secret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                DisplayName = "IdentityPlatform pilot"
            };
            applicationDescriptor.RedirectUris.Add(callback);
            applicationDescriptor.Permissions.UnionWith(PilotProfile.Permissions);
            applicationDescriptor.Requirements.UnionWith(PilotProfile.Requirements);
            applicationDescriptor.Properties["profile"] = JsonSerializer.SerializeToElement(PilotProfile.Id);
            applicationDescriptor.Properties["enabled"] = JsonSerializer.SerializeToElement(true);
            application = await applications.CreateAsync(applicationDescriptor, cancellationToken);
            var scopeDescriptor = new OpenIddictScopeDescriptor { Name = PilotProfile.Scope, DisplayName = "Pilot API" };
            scopeDescriptor.Resources.Add(PilotProfile.Resource);
            scopeDescriptor.Properties["profile"] = JsonSerializer.SerializeToElement(PilotProfile.Id);
            scopeDescriptor.Properties["enabled"] = JsonSerializer.SerializeToElement(true);
            scope = await scopes.CreateAsync(scopeDescriptor, cancellationToken);
            database.Add(new PilotManifest
            {
                UserId = user.Id,
                ApplicationId = (await applications.GetIdAsync(application, cancellationToken))!,
                ScopeId = (await scopes.GetIdAsync(scope, cancellationToken))!,
                UserName = username,
                RedirectUri = redirect
            });
            LoginAudit.Add(database, "pilot-initialization", "created", time.GetUtcNow().ToUnixTimeSeconds(), user.Id, "local-operator");
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
