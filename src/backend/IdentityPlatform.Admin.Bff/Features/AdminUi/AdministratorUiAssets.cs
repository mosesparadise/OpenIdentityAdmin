using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.AdminUi;

/// <summary>Serves only embedded reviewed frontend bytes; no path is expanded into a file or used to construct authority.</summary>
internal static class AdministratorUiAssets
{
    private const string AppScript = "app.js";
    private const string AppStyle = "app.css";
    private const string ThemeScript = "appearance.js";
    private const string Notices = "notices.txt";
    private const string ScriptLicense = "app.js.LEGAL.txt";

    private const string Root = "/";
    private const string Session = "/session";
    private const string Prefix = "/admin-assets/";
    private const string Resource = "IdentityPlatform.Admin.Ui.";
    private const string Html = "text/html; charset=utf-8";
    private const string Css = "text/css; charset=utf-8";
    private const string JavaScript = "text/javascript; charset=utf-8";
    private const string PlainText = "text/plain; charset=utf-8";
    private const string Document = "index.html";
    private const string Policy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'; object-src 'none'";
    private const string MissingAsset = "A required workspace asset is unavailable.";
    private const string AllowedMethods = "GET, HEAD";
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string NoReferrer = "no-referrer";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string Deny = "DENY";
    private static readonly Dictionary<string, Asset> Assets = Load();

    /// <summary>Matches exact casing and paths only; protocol, documentation and unknown routes continue their existing handlers.</summary>
    internal static bool TryGet(PathString path, out Asset asset) => Assets.TryGetValue(path.Value ?? string.Empty, out asset!);

    /// <summary>Rejects method, TLS and framing substitutions while applying restrictive headers to success and denial alike.</summary>
    internal static async Task SendAsync(HttpContext http, Asset asset)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers.ContentSecurityPolicy = Policy;
        http.Response.Headers.XContentTypeOptions = NoSniff;
        http.Response.Headers.XFrameOptions = Deny;
        http.Response.Headers[ReferrerPolicy] = NoReferrer;
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
        {
            http.Response.Headers.Allow = AllowedMethods;
            http.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }
        if (!http.Request.IsHttps || http.Request.QueryString.HasValue || http.Request.ContentType is not null ||
            http.Request.ContentLength is > 0 || http.Request.Headers.TransferEncoding.Count != 0 ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true ||
            http.Features.Get<IHttpRequestFeature>()?.RawTarget is { Length: > 0 } raw && raw != http.Request.Path.Value)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        http.Response.ContentType = asset.Type;
        http.Response.ContentLength = asset.Bytes.Length;
        if (HttpMethods.IsGet(http.Request.Method))
        {
            await http.Response.Body.WriteAsync(asset.Bytes, http.RequestAborted);
        }
    }

    /// <summary>Loads a finite reviewed inventory once; missing embedded production assets fail instead of falling back to source files.</summary>
    private static Dictionary<string, Asset> Load()
    {
        var document = Read(Document, Html);
        return new Dictionary<string, Asset>(StringComparer.Ordinal)
        {
            [Root] = document,
            [Session] = document,
            [Prefix + AppScript] = Read(AppScript, JavaScript),
            [Prefix + AppStyle] = Read(AppStyle, Css),
            [Prefix + ThemeScript] = Read(ThemeScript, JavaScript),
            [Prefix + Notices] = Read(Notices, PlainText),
            [Prefix + ScriptLicense] = Read(ScriptLicense, PlainText)
        };
    }

    /// <summary>Reads only a compile-time-owned manifest resource and retains its immutable content type alongside the bytes.</summary>
    private static Asset Read(string name, string type)
    {
        using var stream = typeof(AdministratorUiAssets).Assembly.GetManifestResourceStream(Resource + name)
            ?? throw new InvalidOperationException(MissingAsset);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new(buffer.ToArray(), type);
    }

    /// <summary>Pairs embedded bytes with the reviewed media type; it contains no request or account data.</summary>
    internal sealed record Asset(byte[] Bytes, string Type);
}
