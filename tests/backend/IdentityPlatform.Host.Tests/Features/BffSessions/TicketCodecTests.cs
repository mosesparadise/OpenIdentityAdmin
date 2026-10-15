using System.Text;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Host.Tests.Features.BffSessions;

/// <summary>Exercises native ticket protection, authenticated metadata and conservative lifetime projection.</summary>
public sealed class TicketCodecTests
{
    /// <summary>Proves native tokens survive restart while caller lifetime properties are privately overridden and rows contain no plaintext.</summary>
    [Fact]
    public void Given_FractionalTimeAndConflictingLifetime_When_RoundTripped_Then_StoreOwnsCanonicalValidity()
    {
        using var fixture = new BffSessionFixture();
        const int IdleMinutes = 5;
        var now = BffSessionFixture.Start.AddTicks(1);
        var row = BffSessionFixture.Row(now);
        var ticket = BffSessionFixture.Ticket();
        ticket.Properties.IssuedUtc = now.AddDays(-1);
        ticket.Properties.ExpiresUtc = now.AddDays(1);
        using (var first = fixture.Protection())
        {
            row.ProtectedTicket = new SessionTicketCodec(first.Provider).Protect(row, ticket);
        }
        Assert.DoesNotContain(BffSessionFixture.TokenSentinel, Encoding.UTF8.GetString(row.ProtectedTicket));
        using var restart = fixture.Protection();
        var result = new SessionTicketCodec(restart.Provider).Unprotect(row);
        Assert.NotNull(result);
        Assert.Equal(BffSessionFixture.TokenSentinel, result.Properties.GetTokenValue(BffSessionFixture.TokenName));
        Assert.Equal(BffSessionFixture.Start, result.Properties.IssuedUtc);
        Assert.Equal(BffSessionFixture.Start.AddMinutes(IdleMinutes), result.Properties.ExpiresUtc);
        Assert.Equal(now.AddDays(1).ToUnixTimeSeconds(), ticket.Properties.ExpiresUtc!.Value.ToUnixTimeSeconds());
        Assert.True(SessionTicketPolicy.IsLive(row, new DateTimeOffset(row.IdleExpiresAt - 1, TimeSpan.Zero)));
        Assert.False(SessionTicketPolicy.IsLive(row, new DateTimeOffset(row.IdleExpiresAt, TimeSpan.Zero)));
    }

    /// <summary>Rejects every altered authentication metadata field and ciphertext corruption without returning native state.</summary>
    [Theory]
    [InlineData(RowDamage.Digest)]
    [InlineData(RowDamage.Subject)]
    [InlineData(RowDamage.Source)]
    [InlineData(RowDamage.Created)]
    [InlineData(RowDamage.Activity)]
    [InlineData(RowDamage.Absolute)]
    [InlineData(RowDamage.IdlePolicy)]
    [InlineData(RowDamage.IdleExpiry)]
    [InlineData(RowDamage.Revision)]
    [InlineData(RowDamage.Ciphertext)]
    public void Given_AlteredRow_When_Unprotected_Then_NoTicketEscapes(RowDamage field)
    {
        using var fixture = new BffSessionFixture();
        using var protection = fixture.Protection();
        var codec = new SessionTicketCodec(protection.Provider);
        var row = BffSessionFixture.Row();
        row.ProtectedTicket = codec.Protect(row, BffSessionFixture.Ticket());
        Damage(row, field);
        Assert.Null(codec.Unprotect(row));
    }

    /// <summary>Changes one authenticated field while retaining the originally protected envelope.</summary>
    private static void Damage(IdentityPlatform.Infrastructure.Features.Sessions.Persistence.AdminSessionTicket row, RowDamage field)
    {
        const string Changed = "changed";
        DamageTime(row, field);
        switch (field)
        {
            case RowDamage.Digest:
                row.Digest = SessionHandle.Create().Digest;
                break;
            case RowDamage.Subject:
                row.Subject = Changed;
                break;
            case RowDamage.Source:
                row.SourceSession = Changed;
                break;
            case RowDamage.Revision:
                row.Revision++;
                break;
            case RowDamage.Ciphertext:
                row.ProtectedTicket[^1] ^= 1;
                break;
        }
    }

    /// <summary>Changes exactly one full-tick time-policy field for substitution controls.</summary>
    private static void DamageTime(IdentityPlatform.Infrastructure.Features.Sessions.Persistence.AdminSessionTicket row, RowDamage field)
    {
        switch (field)
        {
            case RowDamage.Created:
                row.CreatedAt++;
                break;
            case RowDamage.Activity:
                row.LastActivityAt++;
                break;
            case RowDamage.Absolute:
                row.AbsoluteExpiresAt++;
                break;
            case RowDamage.IdlePolicy:
                row.IdleLifetime++;
                break;
            case RowDamage.IdleExpiry:
                row.IdleExpiresAt++;
                break;
        }
    }

    /// <summary>Enumerates independent persisted-field substitutions without combining causes.</summary>
    public enum RowDamage
    {
        Digest, Subject, Source, Created, Activity, Absolute, IdlePolicy, IdleExpiry, Revision, Ciphertext
    }

    /// <summary>Requires named purpose separation and both native and ciphertext size limits.</summary>
    [Fact]
    public void Given_WrongPurposeOrOversizedMaterial_When_Processed_Then_TicketIsRejected()
    {
        using var fixture = new BffSessionFixture();
        using var protection = fixture.Protection();
        var codec = new SessionTicketCodec(protection.Provider);
        var row = BffSessionFixture.Row();
        const string OtherPurpose = "synthetic-other-purpose";
        row.ProtectedTicket = protection.Provider.CreateProtector(OtherPurpose).Protect([1]);
        Assert.Null(codec.Unprotect(row));
        const int OversizedCiphertext = 98_305;
        row.ProtectedTicket = new byte[OversizedCiphertext];
        Assert.Null(codec.Unprotect(row));
        var ticket = BffSessionFixture.Ticket();
        const string PropertyName = "oversized";
        const int OversizedNative = 65_537;
        ticket.Properties.Items[PropertyName] = new string('x', OversizedNative);
        Assert.Throws<ArgumentException>(() => codec.Protect(row, ticket));
    }
    /// <summary>Rejects unsupported envelopes and conflicting native validity even when the envelope has a valid authentication tag.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_AuthenticatedButInvalidEnvelope_When_Loaded_Then_NativeStateIsRejected(bool unsupportedVersion)
    {
        using var fixture = new BffSessionFixture();
        using var protection = fixture.Protection();
        var row = BffSessionFixture.Row();
        const int UnsupportedVersion = 2;
        const string TicketPurpose = "IdentityPlatform.Admin.Bff.SessionTicket.v1";
        var native = BffSessionFixture.Ticket();
        native.Properties.IssuedUtc = SessionTicketPolicy.NativeTime(row.LastActivityAt);
        native.Properties.ExpiresUtc = SessionTicketPolicy.NativeTime(row.IdleExpiresAt);
        var envelope = new SessionTicketCodec.Envelope { Version = 1, State = row, Ticket = BoundedTicketSerialization.Serialize(native) };
        row.ProtectedTicket = protection.Provider.CreateProtector(TicketPurpose)
            .Protect(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope));
        Assert.NotNull(new SessionTicketCodec(protection.Provider).Unprotect(row));
        if (unsupportedVersion)
        {
            envelope.Version = UnsupportedVersion;
        }
        else
        {
            native.Properties.ExpiresUtc = native.Properties.ExpiresUtc!.Value.AddSeconds(1);
            envelope.Ticket = BoundedTicketSerialization.Serialize(native);
        }
        row.ProtectedTicket = protection.Provider.CreateProtector(TicketPurpose)
            .Protect(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope));
        Assert.Null(new SessionTicketCodec(protection.Provider).Unprotect(row));
    }
}
