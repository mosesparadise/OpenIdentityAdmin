using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
LocalRuntimeCredentials? localCredentials = null;
try
{
    var flag = builder.Configuration["LocalDevelopment:ProvisionRuntimeRoles"];
    var provisionLocalRoles = false;
    if (flag is not null && !bool.TryParse(flag, out provisionLocalRoles))
    {
        throw new ArgumentException("Invalid local bootstrap flag.");
    }

    if (provisionLocalRoles)
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new ArgumentException("Local database bootstrap requires Development.");
        }

        localCredentials = new LocalRuntimeCredentials(
            builder.Configuration["LocalDevelopment:PlatformPassword"] ?? "",
            builder.Configuration["LocalDevelopment:SessionsPassword"] ?? "");
        LocalRuntimeRoleProvisioner.ValidatePassword(localCredentials.PlatformPassword);
        LocalRuntimeRoleProvisioner.ValidatePassword(localCredentials.SessionsPassword);
    }
    builder.Services.AddPlatformPersistence(
        DatabaseProviderParser.Parse(builder.Configuration["Database:Provider"]),
        builder.Configuration["ConnectionStrings:platform"] ?? "",
        builder.Configuration["ConnectionStrings:adminsessions"] ?? "");
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid migration configuration. Set Database:Provider and both connection strings.");
    return 2;
}

using var host = builder.Build();
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
return await DatabaseUpgrade.RunAsync(host.Services, deadline.Token, localCredentials);
