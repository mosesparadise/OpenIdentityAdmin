using System.Text.Json.Serialization;

namespace IdentityPlatform.Testing;

/// <summary>Carries bounded synthetic connection and credential settings only through the owned test process's private stdin.</summary>
internal sealed class NativeClientProcessInput
{
    public required string Provider
    {
        get; init;
    }
    public required string RuntimeConnection
    {
        get; init;
    }
    public required Dictionary<string, string?> Credentials
    {
        get; init;
    }
    public required string Issuer
    {
        get; init;
    }
    public required string Client
    {
        get; init;
    }
    public required string Callback
    {
        get; init;
    }
    public required string KeyId
    {
        get; init;
    }
    public long UtcMilliseconds
    {
        get; init;
    }
    public BrowserProcessRequest? Browser
    {
        get; init;
    }
    public string? RequestUri
    {
        get; init;
    }
    public string? Cookie
    {
        get; init;
    }
}

/// <summary>Returns only the actual native request outcome and challenge material through private stdout, without serializing authentication tickets or exceptions.</summary>
internal sealed class NativeClientProcessReply
{
    private const string ResultPhase = "result";
    [JsonPropertyName("phase")]
    public string Phase { get; init; } = ResultPhase;
    public int Status
    {
        get; init;
    }
    public long BodyLength
    {
        get; init;
    }
    public required string Location
    {
        get; init;
    }
    public required string[] Cookies
    {
        get; init;
    }
    public bool Authenticated
    {
        get; init;
    }
    public string? Subject
    {
        get; init;
    }
    public bool Aborted
    {
        get; init;
    }
    public string? BrowserBody
    {
        get; init;
    }
    public int OutboundRequests
    {
        get; init;
    }
    public required string Rejection
    {
        get; init;
    }
}

/// <summary>Restricts a synthetic child to three local product operations rather than accepting arbitrary paths or commands.</summary>
internal enum BrowserProcessOperation
{
    Status = 1, LogoutForm = 2, LogoutSubmit = 3
}

/// <summary>Supplies only explicit synthetic BFF protection and a submit-only native antiforgery field over the private pipe.</summary>
internal sealed class BrowserProcessRequest
{
    public BrowserProcessOperation Operation
    {
        get; init;
    }
    public long UtcTicks
    {
        get; init;
    }
    public required Dictionary<string, string?> Protection
    {
        get; init;
    }
    public string? FormToken
    {
        get; init;
    }
}
