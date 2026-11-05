using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Retains only expiry timestamps from one bounded successful final BFF write, without retaining or reporting its private bytes.</summary>
internal sealed class ApplicationResponseDates
{
    private const int MaximumBytes = 65536;
    private const int MaximumDepth = 16;
    private const string Application = "application";
    private const string Credential = "credential";
    private const string Expires = "expiresAt";
    private readonly object gate = new();
    private bool observed;
    private bool invalid;
    private DateTimeOffset application;
    private DateTimeOffset credential;

    /// <summary>Parses only after successful forwarding; any observation fault invalidates evidence without affecting the HTTP response.</summary>
    internal void Observe(ReadOnlyMemory<byte> bytes)
    {
        lock (gate)
        {
            invalid |= observed || bytes.Length > MaximumBytes;
            observed = true;
            if (invalid)
            {
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(bytes, new()
                {
                    MaxDepth = MaximumDepth
                });
                var details = Unique(document.RootElement, Application);
                application = Unique(details, Expires).GetDateTimeOffset();
                credential = Unique(Unique(details, Credential), Expires).GetDateTimeOffset();
            }
            catch { invalid = true; }
        }
    }

    /// <summary>Marks failed, unsupported or ambiguous writes without throwing into the underlying transport.</summary>
    internal void Reject()
    {
        lock (gate)
        {
            invalid = true;
        }
    }

    /// <summary>Requires both independent published ceilings to exceed the original display deadline strictly.</summary>
    internal bool LaterThan(DateTimeOffset original)
    {
        lock (gate)
        {
            return observed && !invalid && application > original && credential > original;
        }
    }

    /// <summary>Rejects duplicate or missing targeted members without materializing private field values or diagnostic messages.</summary>
    private static JsonElement Unique(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || parent.EnumerateObject().Count(value => value.NameEquals(name)) != 1)
        {
            throw new InvalidDataException();
        }

        return parent.GetProperty(name);
    }
}
