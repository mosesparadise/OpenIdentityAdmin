using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Admits exact bounded JSON mutation framing and distinguishes missing from malformed strong preconditions.</summary>
internal static class ApplicationMutationInput
{
    private const int BodyBytes = 32768;
    private const string JsonType = "application/json";
    private const string JsonUtf8 = "application/json; charset=utf-8";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Reads actual bytes once after path, method and header validation; caller retains strict action-specific DTO validation.</summary>
    internal static async Task<(byte[] Bytes, string? Version)> ReadAsync(HttpContext http, string path, string method, bool precondition, Func<string?, bool>? canonical = null, int malformedStatus = StatusCodes.Status412PreconditionFailed)
    {
        var request = http.Request;
        if (!ApplicationHttpBoundary.Exact(http, path) || request.Method != method ||
            request.ContentType is not (JsonType or JsonUtf8) || request.ContentLength is < 0 or > BodyBytes ||
            request.Headers.ContainsKey(TransferEncoding) || request.Headers.ContainsKey(ContentEncoding) ||
            http.Features.Get<IFormFeature>() is not null)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        var version = ReadVersion(http, precondition, canonical ?? ApplicationVersion.IsCanonical, malformedStatus);
        var bytes = await ApplicationJson.ReadAsync(request.Body, BodyBytes, http.RequestAborted);
        if (request.ContentLength is { } length && length != bytes.Length)
        {
            throw new InvalidDataException();
        }
        return (bytes, version);
    }

    /// <summary>Rejects aliases before persistence while leaving canonical resource/version comparison to the owned native writer.</summary>
    private static string? ReadVersion(HttpContext http, bool required, Func<string?, bool> canonical, int malformedStatus)
    {
        var values = http.Request.Headers.IfMatch;
        if (!required)
        {
            return values.Count == 0 ? null : throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        if (values.Count == 0)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status428PreconditionRequired);
        }
        return values.Count == 1 && canonical(values[0]) ? values[0] :
            throw new ApplicationHttpRejected(malformedStatus);
    }
}
