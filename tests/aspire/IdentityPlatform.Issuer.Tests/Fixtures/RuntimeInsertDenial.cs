using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public sealed class RuntimeInsertDenial : IAsyncDisposable
{
    private readonly IssuerEnvironment environment;
    private readonly AsyncServiceScope scope;
    private readonly PlatformDbContext database;
    private readonly string restore;

    private RuntimeInsertDenial(IssuerEnvironment environment, string provider, bool nativeTokens)
    {
        this.environment = environment;
        scope = environment.Services.CreateAsyncScope();
        database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Database.SetCommandTimeout(5);
        var table = nativeTokens ? "OpenIddictTokens" : "SecurityAuditIntent";
        Deny = provider == "PostgreSql" ? $"REVOKE INSERT ON platform.\"{table}\" FROM platform_runtime" :
            $"DENY INSERT ON OBJECT::[platform].[{table}] TO [platform_runtime]";
        restore = provider == "PostgreSql" ? $"GRANT INSERT ON platform.\"{table}\" TO platform_runtime" :
            $"REVOKE INSERT ON OBJECT::[platform].[{table}] FROM [platform_runtime]";
    }

    private string Deny
    {
        get;
    }

    public static async Task<RuntimeInsertDenial> CreateAsync(IssuerEnvironment environment, string provider, bool nativeTokens = false)
    {
        var denial = new RuntimeInsertDenial(environment, provider, nativeTokens);
        try
        {
            await denial.database.Database.ExecuteSqlRawAsync(denial.Deny, TestContext.Current.CancellationToken);
            return denial;
        }
        catch
        {
            await denial.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await database.Database.ExecuteSqlRawAsync(restore, cleanup.Token);
        }
        catch (Exception) { environment.Invalidate(); }
        finally { await scope.DisposeAsync(); }
        Assert.False(environment.Invalidated, "Runtime INSERT permission restoration failed.");
    }
}
