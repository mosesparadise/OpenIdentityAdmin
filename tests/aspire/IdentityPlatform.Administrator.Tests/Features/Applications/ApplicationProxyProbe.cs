using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Changes only actual HTTPS API responses after obtaining a valid real collection; credentials remain in memory.</summary>
internal sealed class ApplicationProxyProbe
{
    private const string ApiPath = "/api/v1/applications";
    private const string RedirectPath = "/application-test-redirect";
    private const string Json = "application/json";
    private const string Html = "text/html";
    private const string Bearer = "Bearer";
    private const string BearerPrefix = "Bearer ";
    private const string Cookie = "Cookie";
    private const string ForgedIdentity = "X-Identity";
    private const string Canary = "application-response-private-canary";
    private const int OversizedBytes = 524289;
    private const string FirstPage = "\"page\":1";
    private const string DuplicatePage = "\"page\":1,\"page\":2";
    private const string ExtraField = "\"unexpected\":true,";
    private const string ObjectStart = "{";
    private string? access;
    private string? good;
    internal DateTimeOffset UpstreamExpiry
    {
        get; set;
    }
    internal ApplicationProxyFault Fault
    {
        get; set;
    }
    internal int Requests
    {
        get; private set;
    }
    internal int Redirects
    {
        get; private set;
    }
    internal bool CredentialsMatch { get; private set; } = true;
    internal TaskCompletionSource Canceled { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource Started { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Separates caller-cancellation and deadline observations so an earlier completion cannot satisfy a later assertion.</summary>
    internal void BeginDelay()
    {
        Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Fault = ApplicationProxyFault.Delay;
    }

    /// <summary>Seeds response mutations from an actual successfully validated collection instead of a hand-built unproven schema.</summary>
    internal async Task CaptureAsync(Uri api, string credential)
    {
        access = credential;
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(api, ApiPath));
        request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, credential);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        good = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(FirstPage, good, StringComparison.Ordinal);
    }

    /// <summary>Records forwarding invariants and supplies controlled failures from the actual fixed TLS destination.</summary>
    internal async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path == RedirectPath)
        {
            Redirects++;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (Fault == ApplicationProxyFault.None || context.Request.Path != ApiPath)
        {
            await next(context);
            return;
        }
        Requests++;
        CredentialsMatch &= context.Request.Headers.Authorization == BearerPrefix + access &&
            !context.Request.Headers.ContainsKey(Cookie) && !context.Request.Headers.ContainsKey(ForgedIdentity);
        context.Response.ContentType = Json;
        if (Fault == ApplicationProxyFault.Redirect)
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location = RedirectPath;
            return;
        }
        if (Fault == ApplicationProxyFault.Delay)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException) { Canceled.TrySetResult(); }
            return;
        }
        if (Fault == ApplicationProxyFault.Html)
        {
            context.Response.ContentType = Html;
        }
        var body = Fault switch
        {
            ApplicationProxyFault.Valid => good!,
            ApplicationProxyFault.LaterExpiry => WithExpiry(),
            ApplicationProxyFault.Duplicate => good!.Replace(FirstPage, DuplicatePage, StringComparison.Ordinal),
            ApplicationProxyFault.Unexpected => ObjectStart + ExtraField + good![1..],
            ApplicationProxyFault.Oversized => new string(' ', OversizedBytes) + good,
            ApplicationProxyFault.Malformed => ObjectStart,
            _ => Canary
        };
        await context.Response.WriteAsync(body, context.RequestAborted);
    }
    /// <summary>Changes only the already valid response deadline to characterize native client rounding while retaining the original BFF bound.</summary>
    private string WithExpiry()
    {
        const string ExpiresAt = "expiresAt";
        var document = JsonNode.Parse(good!)!;
        document[ExpiresAt] = UpstreamExpiry;
        return document.ToJsonString();
    }
}

/// <summary>Names response-only faults without altering issuer validation, browser credentials or destination selection.</summary>
internal enum ApplicationProxyFault
{
    None, Valid, LaterExpiry, Redirect, Duplicate, Unexpected, Oversized, Malformed, Html, Delay
}
