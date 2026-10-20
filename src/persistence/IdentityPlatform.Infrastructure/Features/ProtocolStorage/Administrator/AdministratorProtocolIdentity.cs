namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Names the single approved administrator protocol profile, independently of any deployment switch.</summary>
internal static class AdministratorProtocolIdentity
{
    internal const string ClientId = "identity-admin-bff";
    internal const string Scope = "identity.admin";
    internal const string Resource = "identity-admin-api";
}
