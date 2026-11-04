using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Captures native lifecycle facts without resolving a document that may already have unloaded.</summary>
internal sealed class ApplicationLifecycleEvents(Action<string, bool, bool> record, Action invalid) : IAsyncDisposable
{
    private const string Chromium = "chromium";
    private const string Binding = "recordApplicationLifecycle";
    private const string BindingCalled = "Runtime.bindingCalled";
    private const string Enable = "Runtime.enable";
    private const string AddBinding = "Runtime.addBinding";
    private const string Name = "name";
    private const string Payload = "payload";
    private const int MaximumPayload = 64;
    private const int FieldCount = 3;
    private const int TrustedIndex = 1;
    private const int PersistedIndex = 2;
    private const string Show = "show";
    private const string Hide = "hide";
    private const string Visible = "visible";
    private const string Hidden = "hidden";
    private const string NativeControl = "[\"hide\",true,false]";
    private const string SyntheticControl = "[\"show\",false,true]";
    private const string Init = """
        (() => {
          if (window !== window.top) return;
          const record = (kind, event) => window.recordApplicationLifecycle(JSON.stringify([kind, event.isTrusted, event.persisted === true]));
          addEventListener('pageshow', e => record('show', e));
          addEventListener('pagehide', e => record('hide', e));
          document.addEventListener('visibilitychange', e => record(document.visibilityState, e));
        })();
        """;
    private ICDPSession? session;

    /// <summary>Chromium forwards the raw synchronous binding event; WebKit retains its proven exposed-function transport.</summary>
    internal async Task AttachAsync(IPage page)
    {
        RequireDecoderControls();
        if (BrowserSession.Engine == Chromium)
        {
            session = await page.Context.NewCDPSessionAsync(page);
            session.Event(BindingCalled).OnEvent += Called;
            await session.SendAsync(Enable);
            await session.SendAsync(AddBinding, new()
            {
                [Name] = Binding
            });
        }
        else
        {
            await page.ExposeFunctionAsync(Binding, (string payload) => { Receive(payload); return true; });
        }
        await page.AddInitScriptAsync(Init);
    }

    /// <summary>Ignores unrelated bindings and never logs raw protocol payloads or execution-context identifiers.</summary>
    private void Called(object? sender, JsonElement? message)
    {
        if (message is not { ValueKind: JsonValueKind.Object } value ||
            !value.TryGetProperty(Name, out var name) || name.ValueKind != JsonValueKind.String)
        {
            invalid();
            return;
        }
        if (name.GetString() != Binding)
        {
            return;
        }
        if (!value.TryGetProperty(Payload, out var payload) || payload.ValueKind != JsonValueKind.String)
        {
            invalid();
            return;
        }
        Receive(payload.GetString()!);
    }

    /// <summary>Accepts only a bounded finite kind and two actual Boolean fields; malformed capture cannot become a pass.</summary>
    private void Receive(string payload)
    {
        if (!TryRead(payload, out var kind, out var trusted, out var persisted))
        {
            invalid();
            return;
        }
        record(kind, trusted, persisted);
    }

    /// <summary>Decodes only the explicit synthetic observation schema, with no arbitrary retained values.</summary>
    private static bool TryRead(string payload, out string kind, out bool trusted, out bool persisted)
    {
        kind = string.Empty;
        trusted = persisted = false;
        if (payload.Length > MaximumPayload)
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(payload);
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != FieldCount ||
                value[0].ValueKind != JsonValueKind.String || value[TrustedIndex].ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                value[PersistedIndex].ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }
            kind = value[0].GetString()!;
            trusted = value[TrustedIndex].GetBoolean();
            persisted = value[PersistedIndex].GetBoolean();
            return kind is Show or Hide or Visible or Hidden;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Runs independent finite positive and malformed/overflow controls before trusting native event capture.</summary>
    private static void RequireDecoderControls()
    {
        Assert.True(TryRead(NativeControl, out var kind, out var trusted, out var persisted));
        Assert.True(kind == Hide && trusted && !persisted);
        Assert.True(TryRead(SyntheticControl, out kind, out trusted, out persisted));
        Assert.True(kind == Show && !trusted && persisted);
        foreach (var invalid in InvalidPayloads)
        {
            Assert.False(TryRead(invalid, out _, out _, out _));
        }
        Assert.False(TryRead(new string(' ', MaximumPayload + 1), out _, out _, out _));
    }

    private static readonly string[] InvalidPayloads = ["null", "{}", "[]", "[\"other\",true,false]", "[\"hide\",1,false]", "[\"hide\",true,null]", "[\"hide\",true,false,0]", "["];

    /// <summary>Releases the owned event subscription and protocol session before the browser is disposed.</summary>
    public async ValueTask DisposeAsync()
    {
        if (session is not null)
        {
            session.Event(BindingCalled).OnEvent -= Called;
            await session.DetachAsync();
        }
    }
}
