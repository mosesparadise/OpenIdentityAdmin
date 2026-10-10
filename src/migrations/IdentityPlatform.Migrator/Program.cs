using IdentityPlatform.Migrator.Composition;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
try
{
    builder.AddConfigurations().ConfigureServices();
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid migration configuration. Set Database:Provider and both connection strings.");
    return 2;
}

using var host = builder.Build();
return await host.RunApplicationAsync();
