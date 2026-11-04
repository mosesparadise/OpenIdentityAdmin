using System.Text.Json;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Records trusted native events synchronously across document destruction using one exact test-owned counter key.</summary>
internal sealed class ApplicationLifecycleEvents(IPage page)
{
    private const string Prefix = "application-lifecycle-witness-";
    private const string Placeholder = "__OWNED_KEY__";
    private const string Seed = "key => { if(sessionStorage.getItem(key)!==null) return false; sessionStorage.setItem(key,'[0,0,0,0,0,0,0]'); return true; }";
    private const string Read = "key => sessionStorage.getItem(key)";
    private const string UntrustedControl = "() => document.dispatchEvent(new Event('visibilitychange'))";
    private const string Remove = "encoded => { const [key,expected]=JSON.parse(encoded); if(sessionStorage.getItem(key)!==expected || typeof window[key]!=='function') return false; window[key](); delete window[key]; sessionStorage.removeItem(key); return sessionStorage.getItem(key)===null; }";
    private const int MaximumPayload = 128;
    private const int MaximumEvents = 256;
    private const int FieldCount = 7;
    private const int HiddenIndex = 1;
    private const int HideIndex = 2;
    private const int ShowIndex = 3;
    private const int PersistedIndex = 4;
    private const int UntrustedIndex = 5;
    private const int FailureIndex = 6;
    private const string EmptyControl = "[0,0,0,0,0,0,0]";
    private const string NativeControl = "[7,1,2,3,1,1,0]";
    private const string Init = """
        (() => {
          if (window !== window.top) return;
          const key = __OWNED_KEY__;
          const read = () => {
            const raw = sessionStorage.getItem(key);
            if (raw === null || raw.length > 128) throw new Error('Invalid lifecycle counter record');
            let value;
            try { value = JSON.parse(raw); } catch { throw new Error('Invalid lifecycle counter record'); }
            if (!Array.isArray(value) || value.length !== 7 || !value.every(n => Number.isInteger(n) && n >= 0 && n <= 256) ||
                value[4] > value[3] || value[1]+value[2]+value[3]+value[5] > value[0] || value[6] !== 0)
              throw new Error('Invalid lifecycle counter record');
            return value;
          };
          const record = (kind, event) => {
            const value = read();
            if (value[0] === 256) value[6] = 1;
            else {
              value[0]++;
              if (!event.isTrusted) value[5]++;
              else if (kind === 'hidden') value[1]++;
              else if (kind === 'hide') value[2]++;
              else if (kind === 'show') { value[3]++; if (event.persisted === true) value[4]++; }
            }
            sessionStorage.setItem(key, JSON.stringify(value));
          };
          const show = e => record('show', e);
          const hide = e => record('hide', e);
          const visibility = e => record(document.visibilityState, e);
          addEventListener('pageshow', show);
          addEventListener('pagehide', hide);
          document.addEventListener('visibilitychange', visibility);
          Object.defineProperty(window, key, {configurable:true, value: () => {
            removeEventListener('pageshow', show); removeEventListener('pagehide', hide);
            document.removeEventListener('visibilitychange', visibility);
          }});
        })();
        """;
    private readonly string key = Prefix + Guid.NewGuid().ToString("N");
    private string last = EmptyControl;

    /// <summary>Seeds once, installs a fresh-read recorder for every document and proves an untrusted event cannot increment native counters.</summary>
    internal async Task AttachAsync()
    {
        RequireDecoderControls();
        Assert.True(await page.EvaluateAsync<bool>(Seed, key));
        var script = Init.Replace(Placeholder, JsonSerializer.Serialize(key), StringComparison.Ordinal);
        await page.AddInitScriptAsync(script);
        await page.EvaluateAsync(script);
        var before = await ReadAsync();
        await page.EvaluateAsync(UntrustedControl);
        var after = await ReadAsync();
        Assert.True(after.Untrusted == before.Untrusted + 1);
        Assert.True(after.Hidden == before.Hidden && after.Hides == before.Hides && after.Shows == before.Shows && after.Persisted == before.Persisted);
    }

    /// <summary>Rejects missing/corrupt/overflow evidence rather than resetting it after navigation or bfcache restoration.</summary>
    internal async Task<ApplicationLifecycleCounters> ReadAsync()
    {
        var raw = await page.EvaluateAsync<string?>(Read, key);
        Assert.True(raw is not null);
        Assert.True(TryRead(raw!, out var counters));
        last = raw!;
        return counters;
    }

    /// <summary>Privately scans the validated record, then atomically detaches the current recorder and removes only that unchanged exact key.</summary>
    internal async Task FinishAsync(string[] secrets)
    {
        await ReadAsync();
        BrowserSecretOracle.RequireAbsent([last], secrets);
        Assert.True(await page.EvaluateAsync<bool>(Remove, JsonSerializer.Serialize(new[] { key, last })));
    }

    /// <summary>Validates independently specified bounded integers and counter relationships before accepting any native observation.</summary>
    private static bool TryRead(string raw, out ApplicationLifecycleCounters counters)
    {
        counters = default;
        if (raw.Length > MaximumPayload)
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(raw);
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != FieldCount)
            {
                return false;
            }
            var numbers = new int[FieldCount];
            for (var index = 0; index < numbers.Length; index++)
            {
                if (value[index].ValueKind != JsonValueKind.Number || !value[index].TryGetInt32(out numbers[index]) || numbers[index] < 0 || numbers[index] > MaximumEvents)
                {
                    return false;
                }
            }
            if (numbers[FailureIndex] != 0 || numbers[PersistedIndex] > numbers[ShowIndex] ||
                numbers[HiddenIndex] + numbers[HideIndex] + numbers[ShowIndex] + numbers[UntrustedIndex] > numbers[0])
            {
                return false;
            }
            counters = new(numbers[HiddenIndex], numbers[HideIndex], numbers[ShowIndex], numbers[PersistedIndex], numbers[UntrustedIndex]);
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Seeds missing, malformed, excess, negative, fractional and contradictory evidence before trusting actual browser observations.</summary>
    private static void RequireDecoderControls()
    {
        Assert.True(TryRead(EmptyControl, out var empty) && empty == default);
        Assert.True(TryRead(NativeControl, out var native));
        Assert.True(native == ExpectedNative);
        foreach (var invalid in InvalidPayloads)
        {
            Assert.False(TryRead(invalid, out _));
        }
        Assert.False(TryRead(new string(' ', MaximumPayload + 1), out _));
    }

    private static readonly ApplicationLifecycleCounters ExpectedNative = new(1, 2, 3, 1, 1);
    private static readonly string[] InvalidPayloads = ["null", "{}", "[]", "[", "[0,0,0,0,0,0,0,0]", "[0,1,0,0,0,0,0]", "[2,0,0,1,2,0,0]",
        "[1,0,0,0,0,0,1]", "[257,0,0,0,0,0,0]", "[-1,0,0,0,0,0,0]", "[0.5,0,0,0,0,0,0]", "[true,0,0,0,0,0,0]"];
}

/// <summary>Contains only validated finite native event counts, never page data or private credentials.</summary>
internal readonly record struct ApplicationLifecycleCounters(int Hidden, int Hides, int Shows, int Persisted, int Untrusted);
