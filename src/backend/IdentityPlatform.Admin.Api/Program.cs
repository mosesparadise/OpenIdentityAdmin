using IdentityPlatform.Admin.Api.Composition;
using IdentityPlatform.Core.Features.Configuration;

try
{
    using var host = HostSetup.Create(args).Build();
    await host.RunAsync();
    return 0;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Invalid database configuration. Set Database:Provider and the required connection string.");
    return 2;
}
catch (LocalDemoConfigurationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
