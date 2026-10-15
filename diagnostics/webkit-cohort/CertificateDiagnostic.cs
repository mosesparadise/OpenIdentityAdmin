namespace IdentityPlatform.Issuer.Tests.Features.Browser;

/// <summary>Scopes a disposable diagnostic driver to the existing certificate session factory.</summary>
internal static class CertificateDiagnostic
{
    private const string Driver = "PLAYWRIGHT_DRIVER_SEARCH_PATH";
    private const string Output = "IDENTITY_WEBKIT_PROVENANCE";

    /// <summary>Restores parent variables before transferring the original session to its original owner.</summary>
    internal static async Task<T> CreateAsync<T>(Func<Task<T>> create) where T : class, IAsyncDisposable
    {
        var priorDriver = Environment.GetEnvironmentVariable(Driver);
        var priorOutput = Environment.GetEnvironmentVariable(Output);
        if (priorDriver is not null || priorOutput is not null)
        {
            throw new InvalidOperationException("Diagnostic ambient driver variables must be absent.");
        }
        var driver = Environment.GetEnvironmentVariable("IDENTITY_CERTIFICATE_DRIVER");
        var output = Environment.GetEnvironmentVariable("IDENTITY_CERTIFICATE_OUTPUT");
        if (string.IsNullOrEmpty(driver) || string.IsNullOrEmpty(output) || !Path.IsPathFullyQualified(output))
        {
            throw new InvalidOperationException("Diagnostic configuration is incomplete.");
        }
        WriteClaim(output + ".armed", "certificate-session-v1");
        T? session = null;
        try
        {
            try
            {
                Environment.SetEnvironmentVariable(Driver, driver);
                Environment.SetEnvironmentVariable(Output, output);
                session = await create();
            }
            finally
            {
                try
                {
                    Environment.SetEnvironmentVariable(Driver, priorDriver);
                }
                finally
                {
                    Environment.SetEnvironmentVariable(Output, priorOutput);
                }
            }
            if (Environment.GetEnvironmentVariable(Driver) is not null || Environment.GetEnvironmentVariable(Output) is not null)
            {
                throw new InvalidOperationException("Diagnostic parent restoration failed.");
            }
            WriteClaim(output + ".restored", "parent-variables-absent-v1");
            return session;
        }
        catch
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }
            throw;
        }
    }

    /// <summary>Rejects duplicate or stale claims without replacing existing evidence.</summary>
    private static void WriteClaim(string path, string token)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(System.Text.Encoding.ASCII.GetBytes(token));
    }
}
