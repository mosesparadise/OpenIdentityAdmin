using System.Security.Cryptography;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Creates a new native identity/hash and product lifecycle within the existing caller-owned transaction and authority receipt.</summary>
internal static class ApplicationCreation
{
    private const int SecretBytes = 32;

    /// <summary>Never adopts existing identities or copies source credentials; a clone starts disabled and retains its independent source verification.</summary>
    internal static async Task<ApplicationWriteState> StageAsync(ProtocolStorageScope scope, IOpenIddictApplicationManager manager,
        ApplicationMutationCommand command, ApplicationCloneSource? source, CancellationToken token)
    {
        await ApplicationMutationWriter.RequireNewIdentityAsync(manager, command.ClientId!, token);
        var now = ApplicationMutationWriter.Now(scope, command);
        var native = command.Kind == ApplicationMutationKind.CreateNative;
        var secret = native ? null : Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes));
        var descriptor = ManagedApplicationProfile.Create(command, secret);
        source?.Apply(descriptor);
        var application = await manager.CreateAsync(descriptor, token);
        var id = await manager.GetIdAsync(application, token) ?? throw ProtocolStorageOwner.Unavailable();
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id == command.ApplicationId)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await ApplicationMutationWriter.RequireMutableAsync(scope, id, command.ClientId!, token);
        var policy = new ApplicationPolicy
        {
            ApplicationId = id,
            Profile = native ? ApplicationEditorRules.NativeProfile : ApplicationEditorRules.Profile,
            Enabled = source is null,
            Revision = 1,
            CredentialRevision = native ? 0 : 1,
            CredentialIssuedAt = native ? null : now,
            CredentialExpiresAt = command.CredentialExpiry?.ToUnixTimeSeconds(),
            ObservedAt = now
        };
        var expected = new ApplicationWriteState(application, descriptor, policy, secret, cloneSource: source);
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(policy);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        return expected;
    }
}
