using System.Net.Http.Headers;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Carries one private bounded send permit through the native service-created scope to its actual HTTP request.</summary>
internal sealed class NativeClientBackchannel
{
    private const string FormMediaType = "application/x-www-form-urlencoded";
    private const string CookieHeader = "Cookie";
    private const string ProxyAuthorization = "Proxy-Authorization";
    private const int MaximumBody = 16 * 1024;
    private const int MaximumFields = 16;
    private const int MaximumKey = 128;
    private const int MaximumValue = 8 * 1024;
    private static readonly AsyncLocal<NativeClientBackchannel?> Current = new();
    private readonly OpenIddictRequest request;
    private readonly OpenIddictClientRegistration registration;
    private readonly OpenIddictConfiguration configuration;
    private readonly Uri destination;
    private readonly Action requireCurrent;
    private readonly string fields;
    private readonly ProtocolStorageRequest operation;
    private bool active = true;
    private int sends;

    /// <summary>Freezes only the exact already admitted native request, destination and associated registration/configuration identities.</summary>
    private NativeClientBackchannel(OpenIddictRequest request, OpenIddictClientRegistration registration,
        OpenIddictConfiguration configuration, Uri destination, Action requireCurrent)
    {
        this.request = request;
        this.registration = registration;
        this.configuration = configuration;
        this.destination = destination;
        this.requireCurrent = requireCurrent;
        fields = Fields(request);
        operation = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        operation.RequireCurrent();
    }

    /// <summary>Owns the full native service invocation; completion cannot succeed without exactly one observed send.</summary>
    internal static async ValueTask RunAsync(OpenIddictRequest request, OpenIddictClientRegistration registration,
        OpenIddictConfiguration configuration, Uri destination, Action requireCurrent, Func<ValueTask> native)
    {
        if (Current.Value is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        requireCurrent();
        var permit = new NativeClientBackchannel(request, registration, configuration, destination, requireCurrent);
        Current.Value = permit;
        try
        {
            await native();
            if (permit.sends != 1)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            requireCurrent();
        }
        finally { permit.active = false; Current.Value = null; }
    }

    /// <summary>Validates actual serialized request bytes immediately inside the replaced native send slot, with no intervening native handler.</summary>
    internal static async ValueTask SendAsync(OpenIddictClientEvents.BaseExternalContext context, Func<ValueTask> native)
    {
        var permit = Current.Value ?? throw ProtocolStorageOwner.Unavailable();
        permit.Require(context);
        if (Interlocked.CompareExchange(ref permit.sends, 1, 0) != 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var http = context.Transaction.GetHttpRequestMessage() ?? throw ProtocolStorageOwner.Unavailable();
        var client = context.Transaction.GetHttpClient() ?? throw ProtocolStorageOwner.Unavailable();
        RequireHeaders(client.DefaultRequestHeaders);
        RequireHttp(http, permit.destination);
        var content = http.Content!;
        var bytes = await content.ReadAsByteArrayAsync(context.CancellationToken);
        if (bytes.Length is 0 or > MaximumBody)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new FormReader(stream) { ValueCountLimit = MaximumFields, KeyLengthLimit = MaximumKey, ValueLengthLimit = MaximumValue };
        var form = await reader.ReadFormAsync(context.CancellationToken);
        if (form.Any(pair => pair.Value.Count != 1) || permit.fields != Digest(form.Select(pair => KeyValuePair.Create(pair.Key, pair.Value[0] ?? throw ProtocolStorageOwner.Unavailable()))))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        permit.Require(context);
        RequireHttp(http, permit.destination);
        RequireHeaders(client.DefaultRequestHeaders);
        if (!ReferenceEquals(http, context.Transaction.GetHttpRequestMessage()) || !ReferenceEquals(content, http.Content) ||
            !ReferenceEquals(client, context.Transaction.GetHttpClient()))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await native();
    }

    /// <summary>Requires a current operation, original objects and unchanged native fields after any nested request handlers.</summary>
    private void Require(OpenIddictClientEvents.BaseExternalContext context)
    {
        operation.RequireCurrent();
        context.CancellationToken.ThrowIfCancellationRequested();
        requireCurrent();
        if (!active || !ReferenceEquals(operation, ProtocolStorageAmbient.Current?.Request) ||
            !ReferenceEquals(Current.Value, this) || !ReferenceEquals(request, context.Transaction.Request) ||
            !ReferenceEquals(registration, context.Registration) || !ReferenceEquals(configuration, context.Configuration) ||
            context.RemoteUri?.OriginalString != destination.AbsoluteUri || fields != Fields(request))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Accepts only the native buffered form POST and fixed HTTPS destination, without alternative credential-bearing headers.</summary>
    private static void RequireHttp(HttpRequestMessage http, Uri destination)
    {
        RequireHeaders(http.Headers);
        if (http.Method != HttpMethod.Post || http.RequestUri?.OriginalString != destination.AbsoluteUri ||
            destination.Scheme != Uri.UriSchemeHttps || http.Content?.GetType() != typeof(FormUrlEncodedContent) ||
            http.Content.Headers.ContentLength is not > 0 or > MaximumBody || http.Content.Headers.ContentType?.MediaType != FormMediaType)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Rejects alternative credentials both on the message and on defaults that HttpClient merges only during SendAsync.</summary>
    private static void RequireHeaders(HttpRequestHeaders headers)
    {
        if (headers.Authorization is not null || headers.Host is not null || headers.Contains(CookieHeader) || headers.Contains(ProxyAuthorization))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Freezes native scalar form semantics independently of the actual HTTP serialization.</summary>
    private static string Fields(OpenIddictRequest request) => Digest(request.GetParameters().Select(pair =>
        KeyValuePair.Create(pair.Key, (string?)pair.Value ?? throw ProtocolStorageOwner.Unavailable())));

    /// <summary>Retains only a canonical digest of field names/values, never a printable assertion/code/verifier snapshot.</summary>
    private static string Digest(IEnumerable<KeyValuePair<string, string>> fields) => FactorBinding.Digest(fields.OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .Select(pair => new { pair.Key, pair.Value }).ToArray());
}
