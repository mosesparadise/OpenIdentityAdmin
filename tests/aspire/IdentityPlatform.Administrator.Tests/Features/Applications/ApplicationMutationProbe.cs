using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Observes genuine fixed HTTPS sends and controls only upstream responses; this is transport evidence, not API mutation acceptance.</summary>
internal sealed class ApplicationMutationProbe
{
    private const string PostMethod = "POST";
    private const string Fractional = "fractional";
    private const string BearerPrefix = "Bearer ";
    private const string CookieHeader = "Cookie";
    private const string CsrfHeader = "X-CSRF-TOKEN";
    private const string IdentityHeader = "X-Identity";
    private const string Pause = "pause";
    private const string Abort = "abort";
    private const string RedirectFault = "redirect";
    private const string Duplicate = "duplicate";
    private const string ClientProperty = "\"clientId\":";
    private const string DuplicateProperty = "\"clientId\":\"private-canary\",\"clientId\":";
    private const string Unknown = "unknown";
    private const string UnknownProperty = "\"private\":\"private-canary\",";
    private const string OversizedFault = "oversized";
    private const string Html = "html";
    private const string HtmlMedia = "text/html";
    private const string Canary = "private-canary";
    private const string CookieResponse = "Set-Cookie";
    private const string UpstreamCookie = "private-canary=upstream; Secure";
    private const string PrivateHeader = "X-Private";
    private const string ExpiryField = "credentialExpiresAt";
    private const string ClientField = "clientId";
    private const string ClientName = "transport-client";
    private const string DisplayField = "displayName";
    private const string DisplayName = "Transport client";
    private const string RedirectsField = "redirectUris";
    private const string Callback = "https://managed.example.test/callback";
    private const string Profile = "managed-web-v1";
    private const string ClientFault = "client";
    private const string OtherClient = "other-client";
    private const string FieldsFault = "fields";
    private const string OtherDisplay = "Other display";
    private const string TagFault = "etag-id";
    private const string OtherId = "other-id";
    private const string RevisionFault = "revision";

    internal const string Id = "managed-transport-result";
    private const string Api = "/api/v1/applications";
    private const string Redirect = "/mutation-redirect-canary";
    private const string Json = "application/json";
    private const int ResponseMinutes = 5;
    private const int EditedRevision = 2;
    private const int WrongRevision = 3;
    private const int SecretBytes = 32;
    private const int Oversized = 524289;
    internal string ExpectedMethod { get; set; } = PostMethod;
    internal string ExpectedPath { get; set; } = Api;
    internal string? ExpectedBody
    {
        get; set;
    }
    internal string? ExpectedVersion
    {
        get; set;
    }
    internal string? Access
    {
        private get; set;
    }
    internal string Fault { get; set; } = string.Empty;
    internal DateTimeOffset Now
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
    internal bool SafeRequest { get; private set; } = true;
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Requires the genuine retained credential and finite forwarding surface, without recording sensitive values.</summary>
    internal async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        if (http.Request.Path == Redirect)
        {
            Redirects++;
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!http.Request.Path.StartsWithSegments(Api) || HttpMethods.IsGet(http.Request.Method))
        {
            await next(http);
            return;
        }
        Requests++;
        SafeRequest &= http.Request.Headers.Authorization.ToString() == BearerPrefix + Access &&
            !http.Request.Headers.ContainsKey(CookieHeader) && !http.Request.Headers.ContainsKey(CsrfHeader) && !http.Request.Headers.ContainsKey(IdentityHeader) &&
            http.Request.ContentType == Json && http.Request.Query.Count == 0 && http.Request.Method == ExpectedMethod &&
            http.Request.Path == ExpectedPath && http.Request.Headers.IfMatch.ToString() == (ExpectedVersion ?? string.Empty);
        using var input = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
        SafeRequest &= ExpectedBody is null || input.RootElement.GetRawText() == ExpectedBody;
        var body = Response(http, input.RootElement);
        if (Fault == Pause)
        {
            Started.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(http.RequestAborted);
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { Canceled.TrySetResult(); throw; }
        }
        if (Fault == Abort)
        {
            http.Abort();
            return;
        }
        if (Fault == RedirectFault)
        {
            http.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            http.Response.Headers.Location = Redirect;
            return;
        }
        if (Fault == Duplicate)
        {
            body = body.Replace(ClientProperty, DuplicateProperty, StringComparison.Ordinal);
        }
        if (Fault == Unknown)
        {
            body = body.Insert(1, UnknownProperty);
        }
        if (Fault == OversizedFault)
        {
            body += new string(' ', Oversized);
        }
        if (Fault == Html)
        {
            http.Response.ContentType = HtmlMedia;
            body = Canary;
        }
        http.Response.Headers[CookieResponse] = UpstreamCookie;
        http.Response.Headers[PrivateHeader] = Canary;
        await http.Response.WriteAsync(body, http.RequestAborted);
    }

    /// <summary>Builds independently expected finite responses from the received wire command and deliberately substitutes selected fields.</summary>
    private string Response(HttpContext http, JsonElement input)
    {
        var create = http.Request.Path == Api;
        var edit = HttpMethods.IsPut(http.Request.Method);
        var issued = DateTimeOffset.FromUnixTimeSeconds(Now.ToUnixTimeSeconds());
        if (Fault == Fractional)
        {
            issued = issued.AddTicks(1);
        }
        var expiry = edit ? issued.AddHours(1) : input.GetProperty(ExpiryField).GetDateTimeOffset();
        var details = new ApplicationDetails(Id, create ? input.GetProperty(ClientField).GetString()! : ClientName,
            edit || create ? input.GetProperty(DisplayField).GetString()! : DisplayName,
            edit || create ? input.GetProperty(RedirectsField).EnumerateArray().Select(row => row.GetString()!).ToArray() : [Callback],
            Profile, true, new(issued, expiry), Now.AddMinutes(ResponseMinutes));
        if (Fault == ClientFault)
        {
            details = details with
            {
                ClientId = OtherClient
            };
        }
        if (Fault == FieldsFault)
        {
            details = details with
            {
                DisplayName = OtherDisplay
            };
        }
        http.Response.StatusCode = create ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        http.Response.ContentType = Json;
        var revision = create ? 1 : long.Parse(http.Request.Headers.IfMatch.ToString().Split('-')[^1].Trim('"'), System.Globalization.CultureInfo.InvariantCulture) + 1;
        http.Response.Headers.ETag = ApplicationVersion.Create(Fault == TagFault ? OtherId : Id, Fault == RevisionFault ? WrongRevision : revision);
        object output = edit ? details : new ApplicationCredentialOutput(details, Convert.ToBase64String(new byte[SecretBytes]));
        return JsonSerializer.Serialize(output, output.GetType(), JsonSerializerOptions.Web);
    }
}
