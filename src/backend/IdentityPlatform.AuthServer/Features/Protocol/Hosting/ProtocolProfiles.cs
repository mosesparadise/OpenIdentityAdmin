using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.AuthServer.Features.Protocol.Hosting;

/// <summary>Freezes the enabled server profiles independently of persisted registrations and request input.</summary>
internal sealed record ProtocolProfiles(bool Pilot, AdministratorRegistrationPolicy? Administrator);
