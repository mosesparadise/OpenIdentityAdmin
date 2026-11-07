namespace IdentityPlatform.Worker.Features.Catalogue;

/// <summary>Composes catalogue deletion using the worker's existing provider and owned protocol storage services.</summary>
internal static class Setup
{
    /// <summary>Adds one bounded durable deletion poller without introducing another host or issuer credentials.</summary>
    internal static WebApplicationBuilder AddCatalogueProcessing(this WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<CatalogueDeletionWorker>();
        return builder;
    }
}
