using System.Security.Cryptography;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Owns an actual TLS pilot callback and disposable client configuration for same-issuer scheme-isolation journeys.</summary>
internal sealed class BrowserPilotClient : IAsyncDisposable
{
    private const string SecretSetting = "Pilot:ClientSecret";
    private const string RedirectSetting = "Pilot:RedirectUri";
    private const int SecretBytes = 32;
    private readonly string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretBytes));
    internal TestRelyingParty Client { get; private set; } = null!;

    /// <summary>Starts the existing genuine relying-party endpoint with trusted TLS before issuer registration freezes its callback.</summary>
    internal static async Task<BrowserPilotClient> CreateAsync()
    {
        var owner = new BrowserPilotClient();
        owner.Client = new TestRelyingParty { ClientSecret = owner.secret };
        try
        {
            await owner.Client.StartAsync(TestContext.Current.CancellationToken);
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Replaces only the disposable fixture's callback and secret before the real initializer creates the native pilot registration.</summary>
    internal void Configure(WebApplicationBuilder builder) => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [SecretSetting] = secret,
        [RedirectSetting] = Client.Callback
    });

    /// <summary>Joins the real pilot listener after dependent browser/issuer fixtures have finished.</summary>
    public ValueTask DisposeAsync() => Client.DisposeAsync();
}
