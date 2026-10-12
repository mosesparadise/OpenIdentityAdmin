using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

/// <summary>Owns isolated native API/BFF fixture hosts and deterministic disposal of their upstream resources.</summary>
internal sealed class ContractHost(WebApplication app, HttpClient client, ContractHost? upstream = null) : IAsyncDisposable
{
    internal WebApplication App { get; } = app;
    internal HttpClient Client { get; } = client;

    /// <summary>Start a real configured contract host, optionally using trusted HTTPS and bounded presentation overrides.</summary>
    internal static async Task<ContractHost> StartAsync(bool browserSurface, bool realHttps = false,
        IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var builder = ContractFixtureConfiguration.CreateBuilder(browserSurface, realHttps, configuration);
        ContractHost? upstream = null;
        WebApplication? app = null;
        HttpClient? client = null;
        try
        {
            upstream = browserSurface ? await StartAsync(false) : null;
            app = builder.Build();
            ContractEndpoints.Map(app, browserSurface, upstream);
            if (realHttps)
            {
                ContractConsumer.Map(app);
            }
            await app.StartAsync(TestContext.Current.CancellationToken);
            client = realHttps ? new HttpClient { BaseAddress = new Uri(app.Urls.Single()) } : app.GetTestClient();
            client.DefaultRequestHeaders.Add(ContractValues.CookieHeader, ContractValues.CookiePair);
            return new(app, client, upstream);
        }
        catch
        {
            client?.Dispose();
            await DisposeHostsAsync(app, upstream);
            throw;
        }
    }

    /// <summary>Dispose client and both hosts even when primary host cleanup fails.</summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await DisposeHostsAsync(App, upstream);
    }

    /// <summary>Always attempt upstream disposal after primary cleanup, including partial startup failures.</summary>
    private static async ValueTask DisposeHostsAsync(WebApplication? app, ContractHost? upstream)
    {
        try
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }
        }
        finally
        {
            if (upstream is not null)
            {
                await upstream.DisposeAsync();
            }
        }
    }
}

internal enum ExampleState
{
    Active, Disabled
}
internal sealed record Example(string Id, string? Label, ExampleState State, DateTimeOffset UpdatedAt, Contact? Contact = null);
internal sealed record ExamplePage(Example[] Items, int PageSize);
internal sealed record ExampleUpdate([property: StringLength(80, MinimumLength = 1)] string Label);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EmailContact), "email")]
[JsonDerivedType(typeof(PhoneContact), "phone")]
internal abstract record Contact;
internal sealed record EmailContact(string Email) : Contact;
internal sealed record PhoneContact(string Phone) : Contact;
