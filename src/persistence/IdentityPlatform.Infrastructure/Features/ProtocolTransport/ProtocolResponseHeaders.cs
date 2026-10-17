using System.Collections;
using System.Globalization;
using System.Text;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Bounds staged headers and rejects mutations after native response start.</summary>
internal sealed class ProtocolResponseHeaders(ProtocolStorageRequest request) : IHeaderDictionary
{
    private readonly Dictionary<string, StringValues> values = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool frozen;
    private readonly object sync = new();
    public StringValues this[string key]
    {
        /// <summary>Matches the native empty-value behavior for absent header names.</summary>
        get
        {
            lock (sync)
            {
                return values.TryGetValue(key, out var value) ? Copy(value) : StringValues.Empty;
            }
        }
        /// <summary>Checks the complete bounded header set before retaining a replacement.</summary>
        set
        {
            lock (sync)
            {
                RequireWritable();
                if (value.Count > ProtocolResponseLimits.HeaderBytes / sizeof(int))
                {
                    throw Reject();
                }
                var bytes = Size(key, value);
                foreach (var pair in values)
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(pair.Key, key))
                    {
                        bytes += Size(pair.Key, pair.Value);
                    }
                }
                if (bytes > ProtocolResponseLimits.HeaderBytes || (!values.ContainsKey(key) && values.Count >= ProtocolResponseLimits.Headers))
                {
                    throw Reject();
                }
                values[key] = Copy(value);
            }
        }
    }
    public long? ContentLength
    {
        /// <summary>Reads the framework content-length header using invariant parsing.</summary>
        get => long.TryParse(this[HeaderNames.ContentLength], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
        /// <summary>Routes typed content length through the same bounded header mutation.</summary>
        set
        {
            if (value is null)
            {
                Remove(HeaderNames.ContentLength);
            }
            else
            {
                this[HeaderNames.ContentLength] = value.Value.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
    public ICollection<string> Keys => Snapshot().Select(pair => pair.Key).ToArray();
    public ICollection<StringValues> Values => Snapshot().Select(pair => pair.Value).ToArray();
    public int Count
    {
        /// <summary>Reads the bounded header count under its mutation lock.</summary>
        get
        {
            lock (sync)
            {
                return values.Count;
            }
        }
    }
    public bool IsReadOnly => frozen;
    /// <summary>Prevents post-start header changes like the real server response.</summary>
    internal void Freeze()
    {
        lock (sync)
        {
            frozen = true;
        }
    }
    /// <summary>Adds one header without silently replacing an existing name.</summary>
    public void Add(string key, StringValues value)
    {
        lock (sync)
        {
            if (values.ContainsKey(key))
            {
                throw Reject();
            }
            this[key] = value;

        }
    }
    /// <summary>Adds a header through the dictionary-compatible interface.</summary>
    public void Add(KeyValuePair<string, StringValues> item) => Add(item.Key, item.Value);
    /// <summary>Removes a header only while the staged response is mutable.</summary>
    public bool Remove(string key)
    {
        lock (sync)
        {
            RequireWritable();
            return values.Remove(key);

        }
    }
    /// <summary>Removes only an exactly matching header pair.</summary>
    public bool Remove(KeyValuePair<string, StringValues> item)
    {
        lock (sync)
        {
            RequireWritable();
            return ((ICollection<KeyValuePair<string, StringValues>>)values).Remove(item);

        }
    }
    /// <summary>Clears staged headers without touching the live response.</summary>
    public void Clear()
    {
        lock (sync)
        {
            RequireWritable();
            values.Clear();

        }
    }
    /// <summary>Reports whether the staged header name exists.</summary>
    public bool ContainsKey(string key)
    {
        lock (sync)
        {
            return values.ContainsKey(key);
        }
    }
    /// <summary>Reports whether the exact staged header pair exists.</summary>
    public bool Contains(KeyValuePair<string, StringValues> item)
    {
        lock (sync)
        {
            return ((ICollection<KeyValuePair<string, StringValues>>)values).Contains(item);
        }
    }
    /// <summary>Reads one staged header without mutation.</summary>
    public bool TryGetValue(string key, out StringValues value)
    {
        lock (sync)
        {
            value = this[key];
            return values.ContainsKey(key);

        }
    }
    /// <summary>Copies staged headers using standard dictionary semantics.</summary>
    public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex) => Snapshot().CopyTo(array, arrayIndex);
    /// <summary>Enumerates the bounded staged set.</summary>
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, StringValues>>)Snapshot()).GetEnumerator();
    /// <summary>Enumerates the staged set through the nongeneric contract.</summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    /// <summary>Rejects late mutation and original request expiry before retaining new data.</summary>
    private void RequireWritable()
    {
        request.RequireCurrent();
        if (frozen)
        {
            throw Reject();
        }
    }
    /// <summary>Copies every multi-value array so callers cannot mutate admitted headers through aliases.</summary>
    private static StringValues Copy(StringValues value) => new(value.ToArray().AsSpan().ToArray());
    /// <summary>Returns a deep enumeration snapshot without exposing the staged header arrays.</summary>
    private KeyValuePair<string, StringValues>[] Snapshot()
    {
        lock (sync)
        {
            return values.Select(pair => new KeyValuePair<string, StringValues>(pair.Key, Copy(pair.Value))).ToArray();
        }
    }
    /// <summary>Counts encoded names and values without retaining another payload copy.</summary>
    private static long Size(string key, StringValues value) => Encoding.UTF8.GetByteCount(key) + ((long)value.Count * sizeof(int)) + value.Sum(item => item is null ? 0L : Encoding.UTF8.GetByteCount(item));
    /// <summary>Latches a terminal failure even if downstream catches the adapter error.</summary>
    private InvalidOperationException Reject()
    {
        request.Fail();
        return ProtocolStorageOwner.Unavailable();
    }
}
