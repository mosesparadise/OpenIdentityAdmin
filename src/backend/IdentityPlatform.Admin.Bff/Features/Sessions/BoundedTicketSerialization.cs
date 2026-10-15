using System.Text;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Enforces the native ticket byte limit while writing, before allocating an arbitrarily large serialized payload.</summary>
internal static class BoundedTicketSerialization
{
    /// <summary>Uses the framework's native writer with a bounded buffer; token and property formats remain framework-owned.</summary>
    internal static byte[] Serialize(AuthenticationTicket ticket)
    {
        using var stream = new TicketBuffer();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        TicketSerializer.Default.Write(writer, ticket);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Rejects writes beyond the native bound without growing the underlying memory buffer.</summary>
    private sealed class TicketBuffer() : MemoryStream(SessionTicketLimits.NativeBytes)
    {
        private const string Failure = "Native session ticket exceeds its size limit.";

        /// <summary>Bounds byte-array writes used by the framework's binary serializer.</summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            RequireCapacity(count);
            base.Write(buffer, offset, count);
        }

        /// <summary>Bounds span writes including native UTF-8 string fragments.</summary>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            RequireCapacity(buffer.Length);
            base.Write(buffer);
        }

        /// <summary>Bounds individual framing bytes without changing native serialization.</summary>
        public override void WriteByte(byte value)
        {
            RequireCapacity(1);
            base.WriteByte(value);
        }

        /// <summary>Rejects overflow before any buffer allocation or write occurs.</summary>
        private void RequireCapacity(int count)
        {
            if (count > SessionTicketLimits.NativeBytes - Position)
            {
                throw new ArgumentException(Failure);
            }
        }
    }
}
