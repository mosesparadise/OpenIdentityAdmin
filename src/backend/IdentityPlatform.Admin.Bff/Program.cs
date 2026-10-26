using IdentityPlatform.Admin.Bff.Composition;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.LocalDemo;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Core.Features.Configuration;

try
{
    using var startup = new StartupContext();
    using var host = HostSetup.Create(args, startup).Build();
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
catch (ClientCredentialFailure)
{
    Console.Error.WriteLine(ClientCredentialFailure.SafeMessage);
    return 2;
}
catch (SessionProtectionException)
{
    Console.Error.WriteLine(SessionProtectionException.SafeMessage);
    return 2;
}
