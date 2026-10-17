namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Serializes native client-state creation, redemption and bounded cleanup in the isolated session database.</summary>
internal sealed class AdministratorClientStateBudget
{
    internal const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    public long Revision
    {
        get; set;
    }
}
