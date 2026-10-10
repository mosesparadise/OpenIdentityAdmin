namespace IdentityPlatform.AuthServer.Composition;

internal sealed class DatabaseConfigurationException : Exception
{
    internal DatabaseConfigurationException() : base("Invalid database configuration. Set Database:Provider and the required connection string.") { }
}
