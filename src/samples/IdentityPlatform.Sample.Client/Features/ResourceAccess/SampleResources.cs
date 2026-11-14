using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IdentityPlatform.Sample.Client.Features.ResourceAccess;

/// <summary>Calls only fixed sample destinations with bounded bodies and returns finite non-credential projections.</summary>
internal sealed class SampleResources(HttpClient client)
{
    private const string UserInfoEndpoint = "connect/userinfo";
    private const string SubjectClaim = "sub";
    private const string EmailClaim = "email";
    private const string EmailVerifiedClaim = "email_verified";
    private const int EmailLimit = 256;
    private const string ProfileReceivedMessage = "Current profile received.";
    private const string OrdersField = "orders";
    private const int OrderFieldCount = 2;
    private const string IdentifierField = "id";
    private const string SyntheticOrder = "sample-order-001";
    private const string StatusField = "status";
    private const string ReadyStatus = "Ready";
    private const string OrdersAcceptedMessage = "Orders API accepted this token: sample-order-001 is Ready.";
    private const string BearerScheme = "Bearer";
    private const string JsonMediaType = "application/json";
    private const int JsonDepth = 4;
    private const string AccessDeniedMessage = "Access denied. Current issuer policy or this resource's token requirements no longer permit the request. Use Sign in to request access again.";
    private const string UnavailableMessage = "The service is unavailable. Return to the sample home page and try again when the service is available. No automatic retry was performed.";

    /// <summary>Retrieves current permitted identity and rejects duplicate fields, unexpected data or subject substitution.</summary>
    internal async Task<ResourceResult> ProfileAsync(string accessToken, string subject, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(SampleProfile.Issuer + UserInfoEndpoint, accessToken, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return Denial(response.StatusCode);
        }
        using var document = await ReadAsync(response, cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var fields = root.EnumerateObject().ToArray();
        if (fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length ||
            fields.Any(field => field.Name is not (SubjectClaim or EmailClaim or EmailVerifiedClaim)) ||
            !root.TryGetProperty(SubjectClaim, out var identifier) || identifier.ValueKind != JsonValueKind.String || identifier.GetString() != subject)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var hasEmail = root.TryGetProperty(EmailClaim, out var email);
        var hasVerified = root.TryGetProperty(EmailVerifiedClaim, out var verified);
        if (hasEmail != hasVerified || hasEmail && (email.ValueKind != JsonValueKind.String || email.GetString() is not { Length: > 0 and <= EmailLimit } ||
            verified.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        return new ResourceResult(ProfileReceivedMessage, subject, hasEmail ? email.GetString() : null, hasVerified ? verified.GetBoolean() : null);
    }

    /// <summary>Accepts only the finite synthetic orders response from the independently validating sample API.</summary>
    internal async Task<ResourceResult> OrdersAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(SampleProfile.Api + OrdersField, accessToken, cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return Denial(response.StatusCode);
        }
        using var document = await ReadAsync(response, cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 ||
            !root.TryGetProperty(OrdersField, out var orders) || orders.ValueKind != JsonValueKind.Array || orders.GetArrayLength() != 1)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        var order = orders[0];
        if (order.ValueKind != JsonValueKind.Object || order.EnumerateObject().Count() != OrderFieldCount ||
            !order.TryGetProperty(IdentifierField, out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != SyntheticOrder ||
            !order.TryGetProperty(StatusField, out var status) || status.ValueKind != JsonValueKind.String || status.GetString() != ReadyStatus)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        return new ResourceResult(OrdersAcceptedMessage);
    }

    /// <summary>Sends a single header-bearer request without following redirects or accepting caller-controlled destinations.</summary>
    private async Task<HttpResponseMessage> SendAsync(string destination, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, destination);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    /// <summary>Bounds actual body bytes even without Content-Length and rejects non-JSON or compressed responses.</summary>
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType != JsonMediaType || response.Content.Headers.ContentEncoding.Count != 0 ||
            response.Content.Headers.ContentLength is > SampleProfile.ResponseLimit)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var bytes = new byte[SampleProfile.ResponseLimit + 1];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
        if (count > SampleProfile.ResponseLimit)
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        return JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = JsonDepth });
    }

    /// <summary>Maps remote failure to fixed user-facing outcomes without reflecting body, header or exception data.</summary>
    private static ResourceResult Denial(HttpStatusCode status) => new(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
        ? AccessDeniedMessage
        : UnavailableMessage);
}

/// <summary>Finite presentation data excludes raw tokens, protocol errors and internal claims.</summary>
internal sealed record ResourceResult(string Message, string? Subject = null, string? Email = null, bool? EmailVerified = null);
