using System.Text;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Retains bounded synthetic diagnostics privately, including structured state and scopes, and exposes only finite privacy assertions.</summary>
internal sealed class NativeClientLogCapture : ILoggerProvider, ISupportExternalScope
{
    private const int CharacterLimit = 4 * 1024 * 1024;
    private const string ControlCategory = "NativeClientPrivacyControl";
    private const string ControlMessage = "Native client diagnostic capture control.";
    private const int ControlEvent = 731;
    private readonly object sync = new();
    private readonly List<string> messages = [];
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
    private int characters;
    private int nativeMessages;
    private bool overflow;

    /// <summary>Preserves every enabled category instead of filtering away potentially private native diagnostics.</summary>
    public ILogger CreateLogger(string categoryName) => new Sink(this, categoryName);

    /// <summary>Uses the logging factory's real scope stack so scope-only private values cannot evade capture.</summary>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    /// <summary>Verifies the configured factory really routes events into this sink before the protocol operation begins.</summary>
    internal void RequireConnected(ILoggerFactory factory)
    {
        factory.CreateLogger(ControlCategory).Log(LogLevel.Trace, new EventId(ControlEvent), ControlMessage, null, static (state, _) => state);
        lock (sync)
        {
            var observed = messages.Any(message => message.Contains(ControlMessage, StringComparison.Ordinal));
            Assert.True(observed);
        }
    }

    /// <summary>Requires real non-control diagnostics and fails closed on capture overflow or any exact private-value occurrence.</summary>
    internal void RequirePrivate(IEnumerable<string> values)
    {
        var secrets = values.Distinct(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(secrets);
        lock (sync)
        {
            Assert.False(overflow);
            Assert.True(nativeMessages > 0);
            Assert.False(Contains(secrets));
        }
    }

    /// <summary>Proves structured-only and scope-only synthetic leaks are detected even when the rendered message masks both values.</summary>
    internal static void RequireDetector()
    {
        const string StructuredSecret = "synthetic-structured-private-value";
        const string ScopeSecret = "synthetic-scoped-private-value";
        const string ExceptionSecret = "synthetic-exception-private-value";
        using var capture = new NativeClientLogCapture();
        using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        var logger = factory.CreateLogger(ControlCategory);
        using (logger.BeginScope(new Dictionary<string, object?> { [nameof(ScopeSecret)] = ScopeSecret }))
        {
            Assert.True(logger.IsEnabled(LogLevel.Trace));
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.Log(LogLevel.Trace, new EventId(ControlEvent), new Dictionary<string, object?> { [nameof(StructuredSecret)] = StructuredSecret },
                    new InvalidOperationException(ExceptionSecret), static (_, _) => ControlMessage);
            }
        }
        Assert.True(capture.Contains([StructuredSecret]));
        Assert.True(capture.Contains([ScopeSecret]));
        Assert.True(capture.Contains([ExceptionSecret]));
        RequireOverflow();
    }

    /// <summary>Proves capture exhaustion remains visible instead of silently truncating a potentially private diagnostic.</summary>
    private static void RequireOverflow()
    {
        using var capture = new NativeClientLogCapture();
        var logger = capture.CreateLogger(ControlCategory);
        var oversized = new string('x', CharacterLimit + 1);
        logger.Log(LogLevel.Trace, new EventId(ControlEvent), oversized, null, static (state, _) => state);
        Assert.True(capture.overflow);
        Assert.Empty(capture.messages);
    }

    /// <summary>Returns only a leak flag from private captured text; callers never receive the offending message.</summary>
    private bool Contains(IEnumerable<string> values) => messages.Any(message => values.Any(value => message.Contains(value, StringComparison.Ordinal)));

    /// <summary>Copies only bounded in-memory text; no captured diagnostic is printed, persisted or returned in failure messages.</summary>
    private void Capture<TState>(string category, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var text = new StringBuilder(formatter(state, exception)).Append(exception?.ToString());
        Append(text, state);
        scopes.ForEachScope(static (scope, buffer) => Append(buffer, scope), text);
        lock (sync)
        {
            if (text.Length > CharacterLimit - characters)
            {
                overflow = true;
                return;
            }
            messages.Add(text.ToString());
            characters += text.Length;
            if (category != ControlCategory)
            {
                nativeMessages++;
            }
        }
    }

    /// <summary>Includes structured values as well as rendered text so unused template fields and logging scopes remain covered.</summary>
    private static void Append(StringBuilder text, object? state)
    {
        text.Append(state);
        if (state is IEnumerable<KeyValuePair<string, object?>> fields)
        {
            foreach (var field in fields)
            {
                text.Append(field.Key).Append(field.Value);
            }
        }
    }

    /// <summary>Releases retained synthetic log text when the owning client factory is disposed.</summary>
    public void Dispose()
    {
        lock (sync)
        {
            messages.Clear();
        }
    }

    /// <summary>Captures every level through the actual logging factory and forwards real scope lifetimes.</summary>
    private sealed class Sink(NativeClientLogCapture owner, string category) : ILogger
    {
        /// <summary>Preserves scope-only values until their real caller disposes the scope.</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);
        /// <summary>Does not suppress trace-level credential diagnostics inside the test sink.</summary>
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        /// <summary>Stores text privately; assertion helpers report only whether an invariant failed.</summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner.Capture(category, state, exception, formatter);
    }
}
