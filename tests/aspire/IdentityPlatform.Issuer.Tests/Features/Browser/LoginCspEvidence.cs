using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal enum LoginCspPhase
{
    Navigation, KeyboardNavigation, Username, Password, ShowPassword, HidePassword, Submit
}

/// <summary>Retains only finite CSP classifications and bounded positions; never page content, URLs or policy samples.</summary>
internal sealed class LoginCspEvidence(IPage page)
{
    private enum Field
    {
        Directive, Source, Line, Column, Phase
    }
    private const int FieldCount = 5;
    private static readonly CompositeFormat EventFormat = CompositeFormat.Parse("{0}; source={1}; line={2}; column={3}; phase={4}");
    private const string OverflowMessage = "CSP event limit exceeded.";
    private const string UnknownPhase = "Unknown";
    private const int MaximumEvents = 16;
    private const int MaximumPosition = 1_000_000;
    private const string Binding = "recordCspViolation";
    private const string SetPhase = "phase => window.__identityCspPhase = phase";
    private const string Observer = """
        window.__identityCspPhase = 0;
        document.addEventListener('securitypolicyviolation', event => {
          const directives = ['style-src-attr', 'style-src-elem', 'script-src-attr', 'script-src-elem', 'form-action', 'connect-src'];
          let source = 0;
          if (event.sourceFile) {
            source = 3;
            try {
              const url = new URL(event.sourceFile);
              if (url.origin === location.origin && url.pathname === '/assets/login.js') source = 1;
              if (url.origin === location.origin && url.pathname === '/assets/theme.js') source = 2;
            } catch {}
          }
          window.recordCspViolation([directives.indexOf(event.effectiveDirective) + 1,
            source, event.lineNumber, event.columnNumber, window.__identityCspPhase]);
        });
        """;
    private static readonly string[] Directives = ["other", "style-src-attr", "style-src-elem", "script-src-attr", "script-src-elem", "form-action", "connect-src"];
    private static readonly string[] Sources = ["empty", "same-origin-login", "same-origin-theme", "other"];
    private readonly List<string> events = [];
    private bool overflow;

    /// <summary>Registers the observer before navigation and projects browser values to finite categories only.</summary>
    internal static async Task<LoginCspEvidence> ObserveAsync(IPage page)
    {
        var evidence = new LoginCspEvidence(page);
        await page.ExposeFunctionAsync(Binding, (int[] values) => evidence.Record(values));
        await page.AddInitScriptAsync(Observer);
        return evidence;
    }

    /// <summary>Marks the next browser action without collecting action values or credential material.</summary>
    internal Task MarkAsync(LoginCspPhase phase) => page.EvaluateAsync(SetPhase, (int)phase);

    /// <summary>Requires zero violations and fails closed if the bounded event buffer overflowed.</summary>
    internal void RequireEmpty()
    {
        lock (events)
        {
            Assert.False(overflow, OverflowMessage);
            Assert.Empty(events);
        }
    }

    /// <summary>Clamps numeric input and emits only local fixed labels; unknown values never become diagnostics.</summary>
    private void Record(int[] values)
    {
        Assert.Equal(FieldCount, values.Length);
        var directive = values[(int)Field.Directive];
        var source = values[(int)Field.Source];
        var line = values[(int)Field.Line];
        var column = values[(int)Field.Column];
        var phase = values[(int)Field.Phase];
        lock (events)
        {
            if (events.Count == MaximumEvents)
            {
                overflow = true;
                return;
            }
            var category = Enum.IsDefined((LoginCspPhase)phase) ? ((LoginCspPhase)phase).ToString() : UnknownPhase;
            var rule = Directives[directive >= 0 && directive < Directives.Length ? directive : 0];
            var origin = Sources[source >= 0 && source < Sources.Length ? source : 0];
            events.Add(string.Format(CultureInfo.InvariantCulture, EventFormat, rule, origin,
                Math.Clamp(line, 0, MaximumPosition), Math.Clamp(column, 0, MaximumPosition), category));
        }
    }
}
