using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Host.Tests.Features.Pilot;

public sealed class PilotFailureTests
{
    [Fact]
    public async Task Unexpected_fault_emits_only_a_fixed_correlated_application_event()
    {
        using var sink = new CaptureProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(sink).SetMinimumLevel(LogLevel.Trace));
        PilotDiagnostics.SuppressProtocolSecrets(services);
        services.AddIdentityPersistence(DatabaseProvider.PostgreSql, "Host=localhost;Database=synthetic");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = body;
        context.Request.Path = "/connect/token";
        context.Request.QueryString = new QueryString("?code=synthetic-code-sentinel");
        context.Request.Headers.Cookie = "synthetic-cookie-sentinel";
        context.TraceIdentifier = "untrusted-trace-sentinel";
        var middleware = ActivatorUtilities.CreateInstance<PilotRequestProtection>(scope.ServiceProvider,
            (RequestDelegate)(_ => throw new InvalidOperationException("synthetic-exception-secret")));
        await middleware.InvokeAsync(context);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        var incident = context.Response.Headers["X-Identity-Incident"].ToString();
        Assert.True(Guid.TryParseExact(incident, "N", out var id) && id != Guid.Empty, "Missing independent incident identifier.");
        Assert.True(sink.Entries.Count == 1, "Expected exactly one operational event.");
        var entry = sink.Entries[0];
        Assert.Equal(typeof(PilotRequestProtection).FullName, entry.Category);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(1001, entry.EventId.Id);
        Assert.True(entry.Exception is null, "Operational event unexpectedly included an exception.");
        Assert.True(entry.Message == $"Pilot request failed. Incident {incident}.", "Unexpected operational event payload.");
        Assert.True(entry.State.Length == 2 && entry.State.Any(value => value.Key == "IncidentId" && Equals(value.Value, incident)) &&
            entry.State.Any(value => value.Key == "{OriginalFormat}" && Equals(value.Value, "Pilot request failed. Incident {IncidentId}.")),
            "Operational state must contain only the fixed template and independent incident identifier.");
        body.Position = 0;
        using var reader = new StreamReader(body);
        Assert.True(await reader.ReadToEndAsync(TestContext.Current.CancellationToken) ==
            "Sign-in is temporarily unavailable. Return to your application and try again.", "Unexpected failure response.");
        using var secondBody = new MemoryStream();
        var second = new DefaultHttpContext { RequestServices = scope.ServiceProvider, TraceIdentifier = context.TraceIdentifier };
        second.Response.Body = secondBody;
        await middleware.InvokeAsync(second);
        var secondIncident = second.Response.Headers["X-Identity-Incident"].ToString();
        Assert.True(Guid.TryParseExact(secondIncident, "N", out var secondId) && secondId != Guid.Empty && secondId != id,
            "Repeated caller correlation must not control the incident identifier.");
        Assert.True(sink.Entries.Count == 2 && sink.Entries[1].Message == $"Pilot request failed. Incident {secondIncident}.",
            "Every unexpected fault must have its own safe operational event.");
    }

    [Fact]
    public async Task Invalid_request_preserves_its_status_without_reporting_a_storage_incident()
    {
        using var sink = new CaptureProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(sink));
        services.AddIdentityPersistence(DatabaseProvider.PostgreSql, "Host=localhost;Database=synthetic");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = body;
        var middleware = ActivatorUtilities.CreateInstance<PilotRequestProtection>(scope.ServiceProvider,
            (RequestDelegate)(_ => throw new BadHttpRequestException("synthetic-private-body", 413)));
        await middleware.InvokeAsync(context);
        Assert.Equal(413, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("X-Identity-Incident"));
        Assert.True(sink.Entries.Count == 0, "A client rejection must not emit an incident.");
    }

    private sealed record Entry(string Category, LogLevel Level, EventId EventId, string Message, Exception? Exception,
        KeyValuePair<string, object?>[] State);

    private sealed class CaptureProvider : ILoggerProvider
    {
        public List<Entry> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Entries);
        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(string category, List<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Add(new(category, logLevel, eventId, formatter(state, exception), exception,
                state is IEnumerable<KeyValuePair<string, object?>> fields ? fields.ToArray() : []));
    }
}
