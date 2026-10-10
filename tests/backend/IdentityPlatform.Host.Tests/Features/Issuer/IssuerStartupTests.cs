using System.Diagnostics;
using System.Net;
using System.Xml.Linq;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed class IssuerStartupTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("pilot-production")]
    [InlineData("pilot-staging")]
    [InlineData("partial")]
    [InlineData("password")]
    [InlineData("ring")]
    [InlineData("missing-deserializer")]
    [InlineData("unknown-decryptor")]
    public async Task Actual_host_verifies_protection_before_listening_without_enabling_protocol_routes(string scenario)
    {
        using var fixture = new IssuerMaterialFixture();
        if (scenario == "partial")
        {
            fixture.Settings.Remove("Identity:Protection:Encryption:Path");
        }

        if (scenario == "password")
        {
            fixture.Settings["Identity:Protection:Signing:Password"] = "synthetic-private-sentinel";
        }

        if (scenario == "ring")
        {
            File.WriteAllText(Path.Combine(fixture.Ring, "invalid.xml"), "<key>synthetic-private-sentinel</key>");
        }

        if (scenario is "missing-deserializer" or "unknown-decryptor")
        {
            using (var provider = CredentialTests.CreateProvider(fixture))
            {
                provider.VerifyIssuerProtection();
            }

            var file = Directory.GetFiles(fixture.Ring, "key-*.xml").Single();
            var xml = XElement.Load(file);
            if (scenario == "missing-deserializer")
            {
                xml.Element("descriptor")!.Attribute("deserializerType")!.Remove();
            }
            else
            {
                xml.Descendants(XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection")).Single().SetAttributeValue("decryptorType", "Unavailable.Type, Unavailable.Assembly");
            }

            xml.Save(file);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(IssuerCredentials).Assembly.Location);
        start.Environment["DOTNET_ENVIRONMENT"] = scenario == "pilot-staging" ? "Staging" : "Production";
        start.Environment["Pilot__Enabled"] = scenario.StartsWith("pilot-", StringComparison.Ordinal) ? "true" : "false";
        start.Environment["Database__Provider"] = "PostgreSql";
        start.Environment["ConnectionStrings__platform"] = "Host=localhost;Port=1;Database=fixture;Username=fixture;Password=synthetic";
        // A real listener allocates its port; no free-port check/bind race.
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        foreach (var entry in fixture.Settings)
        {
            start.Environment[entry.Key.Replace(":", "__")] = entry.Value!;
        }

        using var process = Process.Start(start)!;
        var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = ReadOutput(process.StandardOutput, listening, deadline.Token);
        var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            if (scenario == "valid")
            {
                var address = await listening.Task.WaitAsync(deadline.Token);
                using var client = new HttpClient { BaseAddress = address };
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", deadline.Token)).StatusCode);
                foreach (var path in new[] { "/.well-known/openid-configuration", "/.well-known/jwks", "/connect/jwks", "/connect/authorize", "/connect/token", "/account/login" })
                {
                    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, deadline.Token)).StatusCode);
                }

                Assert.NotEmpty(Directory.GetFiles(fixture.Ring, "key-*.xml"));
            }
            else
            {
                await process.WaitForExitAsync(deadline.Token);
                Assert.Equal(2, process.ExitCode);
                Assert.False(listening.Task.IsCompletedSuccessfully);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanup.Token);
        }
        var combined = await output + await errors;
        Assert.False(combined.Contains("synthetic-private-sentinel", StringComparison.Ordinal), "Startup output disclosed private material.");
        foreach (var secret in fixture.Settings.Where(pair => pair.Key.EndsWith(":Password", StringComparison.Ordinal)).Select(pair => pair.Value!))
        {
            Assert.False(combined.Contains(secret, StringComparison.Ordinal), "Startup output disclosed a certificate password.");
        }

        Assert.False(combined.Contains(fixture.Root, StringComparison.Ordinal), "Startup output disclosed a private material path.");
        if (scenario != "valid")
        {
            Assert.True(combined.Contains(scenario.StartsWith("pilot-", StringComparison.Ordinal)
                ? new PilotConfigurationException().Message : IssuerProtectionException.SafeMessage, StringComparison.Ordinal),
                "Startup must emit the expected fixed configuration error.");
            Assert.False(listening.Task.IsCompletedSuccessfully);
            Assert.False(combined.Contains("Now listening on:", StringComparison.Ordinal), "Invalid startup configuration must not open a listener.");
        }
    }

    private static async Task<string> ReadOutput(StreamReader reader, TaskCompletionSource<Uri> listening, CancellationToken cancellationToken)
    {
        var output = new System.Text.StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            output.AppendLine(line);
            var marker = line.IndexOf("Now listening on: ", StringComparison.Ordinal);
            if (marker >= 0 && Uri.TryCreate(line[(marker + 18)..].Trim(), UriKind.Absolute, out var uri))
            {
                listening.TrySetResult(uri);
            }
        }
        return output.ToString();
    }
}
