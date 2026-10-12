using System.Text;
using System.Text.Json;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;

namespace IdentityPlatform.Host.Tests.Features.Bootstrap;

public sealed class BootstrapInputTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(256)]
    public async Task Accepts_exact_password_boundaries_without_composition_rules(int length)
    {
        var input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            operationId = Guid.NewGuid().ToString("D"),
            operatorReference = "operator-1",
            userName = "admin@example.test",
            password = new string('a', length)
        });
        using var stream = new MemoryStream(input);
        var value = await BootstrapInput.ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(length, value.Password.Length);
        Assert.Equal("admin@example.test", value.UserName);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("{\"unknown\":true}")]
    [InlineData("{}{}")]
    public async Task Invalid_json_shape_has_safe_failure(string json)
    {
        if (json == "duplicate")
        {
            json = "{\"password\":\"canary-secret-value\",\"password\":\"duplicated\"}";
        }
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => BootstrapInput.ReadAsync(stream, TestContext.Current.CancellationToken));
        Assert.False(failure.ToString().Contains("canary-secret-value", StringComparison.Ordinal), "Bootstrap diagnostics disclosed submitted input.");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(257)]
    public async Task Rejects_password_outside_bounds(int length)
    {
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
        {
            operationId = Guid.NewGuid().ToString("D"),
            operatorReference = "operator",
            userName = "admin",
            password = new string('a', length)
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => BootstrapInput.ReadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("operationId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("operationId", "not-a-guid")]
    [InlineData("operatorReference", "bad reference")]
    [InlineData("operatorReference", "")]
    [InlineData("userName", "bad user")]
    [InlineData("userName", "")]
    [InlineData("extra", "unknown-field")]
    public async Task Rejects_invalid_or_unknown_fields(string key, string value)
    {
        var input = new Dictionary<string, string> { ["operationId"] = Guid.NewGuid().ToString("D"), ["operatorReference"] = "operator", ["userName"] = "admin", ["password"] = new string('a', 16) };
        input[key] = key == "extra" ? "canary-secret-value" : value;
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(input));
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => BootstrapInput.ReadAsync(stream, TestContext.Current.CancellationToken));
        Assert.False(failure.ToString().Contains("canary-secret-value", StringComparison.Ordinal), "Bootstrap diagnostics disclosed submitted input.");
    }

    [Theory]
    [InlineData("high")]
    [InlineData("low")]
    [InlineData("property")]
    public async Task Rejects_malformed_escaped_unicode_with_sanitized_input_failure(string scenario)
    {
        var json = scenario switch
        {
            "high" => "{\"password\":\"\\uD800\"}",
            "low" => "{\"password\":\"\\uDC00\"}",
            _ => "{\"\\uD800\":\"unused\"}"
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => BootstrapInput.ReadAsync(stream, TestContext.Current.CancellationToken));
        Assert.Null(failure.InnerException);
        Assert.Equal("Invalid administrator bootstrap input.", failure.Message);
    }

    [Fact]
    public async Task Requires_valid_utf8_and_enforces_raw_byte_limit()
    {
        using var invalid = new MemoryStream([0xFF, 0xFE]);
        await Assert.ThrowsAsync<ArgumentException>(() => BootstrapInput.ReadAsync(invalid, TestContext.Current.CancellationToken));
        var json = JsonSerializer.Serialize(new
        {
            operationId = Guid.NewGuid().ToString("D"),
            operatorReference = "operator",
            userName = "admin",
            password = new string('a', 16)
        });
        var padded = json.PadRight(4096);
        using var exact = new MemoryStream(Encoding.UTF8.GetBytes(padded));
        Assert.NotNull(await BootstrapInput.ReadAsync(exact, TestContext.Current.CancellationToken));
        using var over = new MemoryStream(Encoding.UTF8.GetBytes(padded + " "));
        await Assert.ThrowsAsync<ArgumentException>(() => BootstrapInput.ReadAsync(over, TestContext.Current.CancellationToken));
    }
}
