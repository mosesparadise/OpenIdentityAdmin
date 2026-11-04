using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Separates ordinary readable registration data from deliberately one-time credential output.</summary>
public sealed class ApplicationProjectionTests
{
    private const string SyntheticSecret = "synthetic-one-time-output-value";
    private const string ClientSecretField = "clientSecret";
    private const string CredentialField = "credential";
    private const string ApplicationField = "application";
    private const string ExampleName = "Example";
    private const string ExampleId = "opaque-id";
    private const string ExampleClient = "example-web";
    private const string Callback = "https://client.example/callback";
    private const string Profile = "managed-web-v1";
    private static readonly DateTimeOffset Issued = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] DetailFields = ["clientId", "credential", "displayName", "editable", "expiresAt", "id", "profile", "redirectUris"];
    private static readonly string[] CredentialFields = ["expiresAt", "issuedAt"];
    private static readonly string[] OutputFields = ["application", "clientSecret"];

    /// <summary>Details have an exact finite field set and only lifecycle dates; one-time output serializes separately without a credential-bearing ToString.</summary>
    [Fact]
    public void Given_OneTimeCredential_When_Serialized_Then_OnlyExplicitOutputContainsPlaintext()
    {
        var details = new ApplicationDetails(ExampleId, ExampleClient, ExampleName, [Callback], Profile, true,
            new(Issued, Issued.AddDays(1)), Issued.AddMinutes(1));
        var output = new ApplicationCredentialOutput(details, SyntheticSecret);
        using var safe = JsonDocument.Parse(JsonSerializer.Serialize(details, JsonSerializerOptions.Web));
        Assert.Equal(DetailFields, safe.RootElement.EnumerateObject().Select(value => value.Name).Order());
        Assert.Equal(CredentialFields, safe.RootElement.GetProperty(CredentialField).EnumerateObject().Select(value => value.Name).Order());
        Assert.False(safe.RootElement.TryGetProperty(ClientSecretField, out _));
        using var once = JsonDocument.Parse(JsonSerializer.Serialize(output, JsonSerializerOptions.Web));
        Assert.Equal(OutputFields, once.RootElement.EnumerateObject().Select(value => value.Name).Order());
        Assert.True(once.RootElement.GetProperty(ClientSecretField).GetString() == SyntheticSecret);
        Assert.Equal(safe.RootElement.GetRawText(), once.RootElement.GetProperty(ApplicationField).GetRawText());
        Assert.DoesNotContain(SyntheticSecret, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticSecret, details.ToString(), StringComparison.Ordinal);
    }
}
