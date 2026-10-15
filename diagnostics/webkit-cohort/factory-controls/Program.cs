using IdentityPlatform.Issuer.Tests.Features.Browser;

var directory = Path.Combine(Path.GetTempPath(), "identity-factory-controls-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string[] names = ["PLAYWRIGHT_DRIVER_SEARCH_PATH", "IDENTITY_WEBKIT_PROVENANCE", "IDENTITY_CERTIFICATE_DRIVER", "IDENTITY_CERTIFICATE_OUTPUT"];
var inherited = names.Select(Environment.GetEnvironmentVariable).ToArray();
var controls = 0;
void Require(bool value) { if (!value) throw new Exception("Factory control failed."); }
void Reset()
{
    foreach (var name in names) Environment.SetEnvironmentVariable(name, null);
    Environment.SetEnvironmentVariable(names[2], directory);
    Environment.SetEnvironmentVariable(names[3], Path.Combine(directory, Guid.NewGuid().ToString("N")));
}
void Restored() { Require(names.Take(2).All(n => Environment.GetEnvironmentVariable(n) is null)); }
async Task Reject(Func<Task<Session>> action)
{
    try { await action(); throw new Exception("Unexpected success."); }
    catch (Exception e) when (e.Message != "Unexpected success.") { ++controls; }
}
try
{
    Reset(); var made = new Session();
    var returned = await CertificateDiagnostic.CreateAsync(() =>
    {
        Require(Environment.GetEnvironmentVariable(names[0]) == directory);
        Require(Environment.GetEnvironmentVariable(names[1]) == Environment.GetEnvironmentVariable(names[3]));
        return Task.FromResult(made);
    });
    Require(ReferenceEquals(made, returned) && !made.Disposed); Restored(); ++controls;
    await returned.DisposeAsync();
    await Reject(() => CertificateDiagnostic.CreateAsync(() => Task.FromResult(new Session()))); Restored();
    Reset(); await Reject(() => CertificateDiagnostic.CreateAsync<Session>(() => throw new InvalidOperationException())); Restored();
    Reset(); await Reject(() => CertificateDiagnostic.CreateAsync<Session>(async () => { await Task.Yield(); throw new InvalidOperationException(); })); Restored();
    Reset(); await Reject(() => CertificateDiagnostic.CreateAsync<Session>(() => Task.FromCanceled<Session>(new CancellationToken(true)))); Restored();
    Reset(); Environment.SetEnvironmentVariable(names[0], "ambient");
    await Reject(() => CertificateDiagnostic.CreateAsync(() => Task.FromResult(new Session())));
    Require(Environment.GetEnvironmentVariable(names[0]) == "ambient" && Environment.GetEnvironmentVariable(names[1]) is null);
    Reset(); Environment.SetEnvironmentVariable(names[1], "ambient");
    await Reject(() => CertificateDiagnostic.CreateAsync(() => Task.FromResult(new Session())));
    Require(Environment.GetEnvironmentVariable(names[1]) == "ambient" && Environment.GetEnvironmentVariable(names[0]) is null);
    Reset(); Environment.SetEnvironmentVariable(names[3], null);
    await Reject(() => CertificateDiagnostic.CreateAsync(() => Task.FromResult(new Session()))); Restored();
    Reset(); Environment.SetEnvironmentVariable(names[3], "relative");
    await Reject(() => CertificateDiagnostic.CreateAsync(() => Task.FromResult(new Session()))); Restored();
    Reset(); made = new Session();
    File.WriteAllText(Environment.GetEnvironmentVariable(names[3]) + ".restored", "stale");
    await Reject(() => CertificateDiagnostic.CreateAsync(() => Task.FromResult(made)));
    Restored(); Require(made.Disposed);
    Console.WriteLine($"Certificate factory controls: {controls} passed; no browser or database executed.");
}
finally
{
    for (var i = 0; i < names.Length; ++i) Environment.SetEnvironmentVariable(names[i], inherited[i]);
    Directory.Delete(directory, true);
}
sealed class Session : IAsyncDisposable
{
    internal bool Disposed;
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}
