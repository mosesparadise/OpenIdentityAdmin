using OpenIddict.Client.SystemNetHttp;
using static OpenIddict.Client.OpenIddictClientEvents;

namespace IdentityPlatform.Sample.Client.Features.Authentication;

/// <summary>Rejects encoded native responses before the library's independent decompression can exceed the wire buffer bound.</summary>
internal static class SampleTransport
{
    private const string ServerError = "server_error";
    private const int HandlerOrderOffset = 500;

    /// <summary>Guards each external response used by this fixed sample profile without replacing native parsing or validation.</summary>
    internal static void Add(OpenIddictClientBuilder builder)
    {
        RejectEncoding<ExtractConfigurationResponseContext>(builder);
        RejectEncoding<ExtractJsonWebKeySetResponseContext>(builder);
        RejectEncoding<ExtractTokenResponseContext>(builder);
    }

    /// <summary>Runs after bounded HTTP receipt and before native decompression, rejecting every content encoding.</summary>
    private static void RejectEncoding<TContext>(OpenIddictClientBuilder builder) where TContext : BaseExternalContext =>
        builder.AddEventHandler<TContext>(handler => handler.UseInlineHandler(context =>
        {
            var response = context.Transaction.GetHttpResponseMessage() ?? throw new InvalidOperationException(SampleProfile.Failure);
            if (response.Content.Headers.ContentEncoding.Count > 0)
            {
                response.Dispose();
                context.Reject(error: ServerError, description: SampleProfile.Failure);
            }
            return default;
        }).SetOrder(OpenIddictClientSystemNetHttpHandlers.DecompressResponseContent<TContext>.Descriptor.Order - HandlerOrderOffset));
}
