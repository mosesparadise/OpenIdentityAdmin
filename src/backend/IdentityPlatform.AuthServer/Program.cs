using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Pilot;

using var startup = new StartupContext(args);
try
{
    await using var app = WebApplication.CreateBuilder(startup.HostArguments)
        .AddConfigurations()
        .ConfigureServices(startup)
        .Build();

    app.ConfigureApplication(startup);
    return await app.RunApplicationAsync(startup);
}
catch (DatabaseConfigurationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (IssuerProtectionException)
{
    Console.Error.WriteLine(IssuerProtectionException.SafeMessage);
    return 2;
}
catch (PilotConfigurationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
