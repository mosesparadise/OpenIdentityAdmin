using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Retains private test-only database/store diagnostics without printing their content into assertion messages.</summary>
internal sealed class SessionLogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> messages = new();

    /// <summary>Captures every category so absence of private sentinels is not caused by a filtered test logger.</summary>
    public ILogger CreateLogger(string categoryName) => new Sink(messages);

    /// <summary>Requires actual captured diagnostics and rejects private values without echoing the offending message.</summary>
    internal void RequireNoPrivateValues(params string[] privateValues)
    {
        Assert.NotEmpty(messages);
        var privateValueObserved = messages.Any(message => privateValues.Any(value => message.Contains(value, StringComparison.Ordinal)));
        Assert.False(privateValueObserved);
    }

    /// <summary>No external resources are owned; retained diagnostics remain private to this fixture.</summary>
    public void Dispose()
    {
    }

    /// <summary>Captures formatted messages and exception text only inside the synthetic fixture.</summary>
    private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
    {
        /// <summary>Scopes do not change capture coverage or retain additional private objects.</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <summary>Includes trace through critical events in the privacy test.</summary>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <summary>Stores diagnostics privately so callers can assert only a boolean sentinel absence.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception) + exception?.ToString());
    }
}
