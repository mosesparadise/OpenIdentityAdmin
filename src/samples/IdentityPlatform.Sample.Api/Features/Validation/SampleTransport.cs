using OpenIddict.Validation;
using OpenIddict.Validation.SystemNetHttp;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace IdentityPlatform.Sample.Api.Features.Validation;

/// <summary>Rejects encoded native responses before the library's independent decompression can exceed the wire buffer bound.</summary>
internal static class SampleTransport
{
    private const string ServerError = "server_error";
    private const int HandlerOrderOffset = 500;

    /// <summary>Guards each external response used by this fixed sample profile without replacing native parsing or validation.</summary>
    internal static void Add(OpenIddictValidationBuilder builder)
    {
        RejectEncoding<ExtractConfigurationResponseContext>(builder);
        RejectEncoding<ExtractJsonWebKeySetResponseContext>(builder);
    }

    /// <summary>Runs after bounded HTTP receipt and before native decompression, rejecting every content encoding.</summary>
    private static void RejectEncoding<TContext>(OpenIddictValidationBuilder builder) where TContext : BaseExternalContext =>
        builder.AddEventHandler<TContext>(handler => handler.UseInlineHandler(context =>
        {
            var response = context.Transaction.GetHttpResponseMessage() ?? throw new InvalidOperationException(SampleProfile.Failure);
            if (response.Content.Headers.ContentEncoding.Count > 0)
            {
                response.Dispose();
                context.Reject(error: ServerError, description: SampleProfile.Failure);
            }
            return default;
        }).SetOrder(OpenIddictValidationSystemNetHttpHandlers.DecompressResponseContent<TContext>.Descriptor.Order - HandlerOrderOffset));
}
