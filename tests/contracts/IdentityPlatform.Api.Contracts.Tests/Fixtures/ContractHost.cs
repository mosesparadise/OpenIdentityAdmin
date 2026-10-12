using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ApiContracts = IdentityPlatform.Admin.Api.Features.ApiContracts.Setup;
using BffContracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;

namespace IdentityPlatform.Api.Contracts.Tests.Fixtures;

internal sealed class ContractHost(WebApplication app, HttpClient client, ContractHost? upstream = null) : IAsyncDisposable
{
    internal WebApplication App { get; } = app;
    internal HttpClient Client { get; } = client;

    internal static async Task<ContractHost> StartAsync(bool browserSurface, bool realHttps = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        if (realHttps)
        {
            builder.WebHost.UseUrls("https://127.0.0.1:0");
        }
        else
        {
            builder.WebHost.UseTestServer();
        }
        if (browserSurface)
        {
            BffContracts.AddApiContracts(builder);
        }
        else
        {
            ApiContracts.AddApiContracts(builder);
        }
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<ExampleState>()));
        builder.Services.Configure<OpenApiOptions>(browserSurface ? BffContracts.DocumentName : ApiContracts.DocumentName,
            options => options.AddDocumentTransformer((document, _, _) =>
            {
                FixtureMetadata.Apply(document, browserSurface);
                return Task.CompletedTask;
            }));
        var upstream = browserSurface ? await StartAsync(false) : null;
        var app = builder.Build();
        var group = browserSurface ? BffContracts.MapManagementGroup(app) : ApiContracts.MapManagementGroup(app);
        if (browserSurface)
        {
            group.AddEndpointFilter(async (context, next) =>
            {
                var request = context.HttpContext.Request;
                if (request.Cookies["__Host-contract-fixture"] != "synthetic-session")
                {
                    return Results.Problem(statusCode: 401);
                }
                if (HttpMethods.IsPut(request.Method) && request.Headers["X-CSRF-TOKEN"] != "synthetic-proof")
                {
                    return Results.Problem(statusCode: 403);
                }
                return await next(context);
            });
        }
        group.MapGet("/examples", ([FromQuery, Range(1, 100)] int pageSize = 25) =>
                pageSize is < 1 or > 100 ? Results.Problem(statusCode: 400) : Results.Ok(new ExamplePage([new("fixture", null, ExampleState.Active, DateTimeOffset.UnixEpoch)], pageSize)))
            .WithName("ListExamples").WithSummary("List synthetic examples")
            .Produces<ExamplePage>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        group.MapGet("/examples/{id}", (string id, HttpContext context) =>
        {
            context.Response.Headers.ETag = "\"fixture-v1\"";
            return new Example(id, null, ExampleState.Active, DateTimeOffset.UnixEpoch);
        }).WithName("GetExample").WithSummary("Get a synthetic example")
            .Produces<Example>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        group.MapPut("/examples/{id}", (string id, ExampleUpdate update, HttpContext context) =>
        {
            if (!context.Request.Headers.ContainsKey("If-Match"))
            {
                return Results.Problem(statusCode: 428);
            }
            return Results.Ok(new Example(id, update.Label, ExampleState.Active, DateTimeOffset.UnixEpoch));
        }).WithName("UpdateExample").WithSummary("Update a synthetic example")
            .Produces<Example>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(404).ProducesProblem(409).ProducesProblem(412).ProducesProblem(428);
        app.MapGet("/alive", () => "Healthy");
        if (upstream is not null)
        {
            app.MapGet("/openapi/v1.json", async (CancellationToken cancellationToken) => Results.Content(
                await upstream.Client.GetStringAsync("/openapi/v1.json", cancellationToken), "application/json")).ExcludeFromDescription();
        }
        if (browserSurface)
        {
            IdentityPlatform.Admin.Bff.Features.Documentation.Setup.MapDocumentation(app);
        }
        else
        {
            IdentityPlatform.Admin.Api.Features.Documentation.Setup.MapDocumentation(app);
        }
        if (realHttps)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "global.json")))
            {
                root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
            }
            var script = File.ReadAllText(Path.Combine(root.FullName, "src/frontend/IdentityPlatform.Admin.Contracts/dist/consumer.js"));
            app.MapGet("/consumer.js", () => Results.Content(script, "text/javascript")).ExcludeFromDescription();
            app.MapGet("/consumer", (HttpContext context) =>
            {
                context.Response.Cookies.Append("__Host-contract-fixture", "synthetic-session", new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                });
                return Results.Content("<!doctype html><html lang=\"en\"><title>Synthetic consumer</title><script src=\"/consumer.js\"></script><body>Contract consumer</body></html>", "text/html");
            }).ExcludeFromDescription();
        }
        await app.StartAsync(TestContext.Current.CancellationToken);
        var client = realHttps ? new HttpClient { BaseAddress = new Uri(app.Urls.Single()) } : app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", "__Host-contract-fixture=synthetic-session");
        return new(app, client, upstream);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
        if (upstream is not null)
        {
            await upstream.DisposeAsync();
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
