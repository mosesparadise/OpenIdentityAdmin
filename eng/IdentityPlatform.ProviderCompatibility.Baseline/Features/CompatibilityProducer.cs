using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.ProviderCompatibility.Baseline.Features;

/// <summary>Builds only against frozen pre-refactor assemblies to populate disposable compatibility databases.</summary>
internal static class CompatibilityProducer
{
    private const string ProviderSetting = "COMPATIBILITY_PROVIDER";
    private const string ConnectionSetting = "COMPATIBILITY_CONNECTION";
    private const string UserId = "provider-compatibility-user";
    private const string UserName = "provider-compatibility@example.test";
    private const string ClientId = "provider-compatibility-client";
    private const string SessionId = "provider-compatibility-session";
    private const string Failure = "Frozen baseline compatibility preparation failed.";
    private const int DeadlineMinutes = 3;
    private const string LegacyParserMethod = "Parse";
    private const string LegacyPersistenceMethod = "AddPlatformPersistence";

    /// <summary>Uses the old implementation's migrator and native managers, returning only a safe process outcome.</summary>
    internal static async Task<int> RunAsync()
    {
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(DeadlineMinutes));
            var connection = Environment.GetEnvironmentVariable(ConnectionSetting) ?? string.Empty;
            var services = LegacyServices(connection);
            await using var root = services.BuildServiceProvider(validateScopes: true);
            if (await DatabaseUpgrade.RunAsync(root, deadline.Token) != 0)
            {
                return 1;
            }
            await using var scope = root.CreateAsyncScope();
            await SeedPlatformAsync(scope.ServiceProvider, deadline.Token);
            await SeedSessionsAsync(scope.ServiceProvider, deadline.Token);
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(Failure);
            return 1;
        }
    }

    /// <summary>Requires the frozen binary's old public signatures, so a normal current-tree build cannot masquerade as the baseline.</summary>
    private static IServiceCollection LegacyServices(string connection)
    {
        var parser = typeof(DatabaseProviderParser).GetMethod(LegacyParserMethod, [typeof(string)])
            ?? throw new InvalidOperationException(Failure);
        var provider = parser.Invoke(null, [Environment.GetEnvironmentVariable(ProviderSetting)]);
        var registration = typeof(PersistenceRegistration).GetMethod(LegacyPersistenceMethod)
            ?? throw new InvalidOperationException(Failure);
        var services = new ServiceCollection().AddLogging();
        registration.Invoke(null, [services, provider, connection, connection]);
        return services;
    }

    /// <summary>Creates native Identity/OpenIddict records and a product user flag with the frozen implementation.</summary>
    private static async Task SeedPlatformAsync(IServiceProvider services, CancellationToken token)
    {
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var user = new PlatformUser { Id = UserId, UserName = UserName, IsEnabled = false };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(Failure);
        }
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public
        }, token);
    }

    /// <summary>Creates isolated native session state using the frozen session model and database implementation.</summary>
    private static async Task SeedSessionsAsync(IServiceProvider services, CancellationToken token)
    {
        var database = services.GetRequiredService<AdminSessionDbContext>();
        database.Add(new OpenIddictEntityFrameworkCoreToken
        {
            Id = SessionId,
            Subject = UserId,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken
        });
        await database.SaveChangesAsync(token);
    }
}
