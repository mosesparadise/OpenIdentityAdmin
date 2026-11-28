using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Sample.Api.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Validation;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Sends the issuer's real signed service credential through the shipped orders resource server's HTTP and authentication pipeline.</summary>
internal static class MachineResourceTest
{
    private const string Resource = "https://orders.dev.localhost:17447/";
    private const string Path = "orders";
    private const string SampleOrder = "sample-order-001";
    private const string Order = "id";
    private const string Enabled = "LocalDemo:Enabled";
    private const string True = "true";
    private const string Bearer = "Bearer";

    /// <summary>Supplies the actual test issuer and its discovered public keys to the sample without copying private material.</summary>
    internal static async Task RequireAsync(string token, string issuer, IList<SecurityKey> signingKeys)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Enabled] = True });
        builder.WebHost.UseTestServer();
        builder.Services.PostConfigure<OpenIddictValidationOptions>(options =>
        {
            options.Issuer = new Uri(issuer);
            options.TokenValidationParameters.ValidIssuers = [issuer];
            options.Configuration = new OpenIddictConfiguration { Issuer = options.Issuer };
            foreach (var key in signingKeys)
            {
                options.Configuration.SigningKeys.Add(key);
            }
        });
        await using var app = SampleApplication.Create(builder);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(Resource);
        using var anonymous = await client.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, token);
        using var accepted = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var json = JsonDocument.Parse(await accepted.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SampleOrder, Assert.Single(json.RootElement.GetProperty(Path).EnumerateArray()).GetProperty(Order).GetString());
    }
}
