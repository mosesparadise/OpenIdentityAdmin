using IdentityPlatform.Admin.Api.Composition;

var builder = WebApplication.CreateBuilder(args).AddConfigurations();
try
{
    builder.ConfigureServices();
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid database configuration. Set Database:Provider and the required connection string.");
    return 2;
}

var app = builder.Build().ConfigureApplication();
await app.RunAsync();
return 0;
