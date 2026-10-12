using IdentityPlatform.Migrator.Composition;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
try
{
    builder.AddConfigurations().ConfigureServices();
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid migration configuration. Check operation, provider and required connections.");
    return 2;
}

using var host = builder.Build();
return await host.RunApplicationAsync();
