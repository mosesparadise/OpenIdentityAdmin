using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Rejects ambiguous audit metadata and keeps the private native sender's provenance immutable.</summary>
public sealed class BrowserAuditCorrelationTests
{
    private const string Format = "N";
    private const string Removed = "remove";
    private const string Replaced = "replace";
    private const string Duplicated = "duplicate";
    private const string Defaults = "defaults";
    private const string Canonical = "0123456789abcdef0123456789abcdef";
    private const string Other = "1123456789abcdef0123456789abcdef";
    private const string Header = "Identity-Browser-Audit-Correlation";

    /// <summary>Canonical parsing does not normalize ambiguous, empty or duplicate wire representations.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData(" 0123456789abcdef0123456789abcdef")]
    [InlineData(Canonical + "," + Canonical)]
    public void Given_NoncanonicalWireValue_When_AuditCorrelationIsRead_Then_NoProvenanceIsAccepted(string value)
    {
        Assert.Null(BrowserAuditCorrelation.Read(value));
        Assert.Null(BrowserAuditCorrelation.Read(new StringValues([Canonical, Canonical])));
        Assert.Null(BrowserAuditCorrelation.Read(StringValues.Empty));
        Assert.Equal(Guid.ParseExact(Canonical, Format), BrowserAuditCorrelation.Read(Canonical));
    }

    /// <summary>Existing message or client-default metadata cannot be silently promoted into a private native send permit.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_UnownedHeader_When_NativeSenderAttachesCorrelation_Then_DenialPrecedesTransmission(bool defaults)
    {
        using var request = new HttpRequestMessage();
        using var client = new HttpClient();
        var headers = defaults ? client.DefaultRequestHeaders : request.Headers;
        headers.Add(Header.ToLowerInvariant(), Canonical);
        Assert.Throws<InvalidOperationException>(() => NativeClientAuditHeader.Attach(request.Headers, client.DefaultRequestHeaders, Guid.ParseExact(Canonical, Format)));
    }

    /// <summary>The exact owned value survives checks while removal, substitution, duplication and late defaults are rejected.</summary>
    [Theory]
    [InlineData(Removed)]
    [InlineData(Replaced)]
    [InlineData(Duplicated)]
    [InlineData(Defaults)]
    public void Given_OwnedParHeader_When_TransportMetadataChanges_Then_FrozenPermitRejectsIt(string fault)
    {
        using var request = new HttpRequestMessage();
        using var client = new HttpClient();
        var correlation = Guid.ParseExact(Canonical, Format);
        NativeClientAuditHeader.Attach(request.Headers, client.DefaultRequestHeaders, correlation);
        NativeClientAuditHeader.Require(request.Headers, client.DefaultRequestHeaders, correlation);
        if (fault is Removed or Replaced)
        {
            request.Headers.Remove(Header);
        }
        if (fault == Replaced)
        {
            request.Headers.Add(Header, Other);
        }
        if (fault == Duplicated)
        {
            request.Headers.Add(Header, Canonical);
        }
        if (fault == Defaults)
        {
            client.DefaultRequestHeaders.Add(Header, Canonical);
        }
        Assert.Throws<InvalidOperationException>(() => NativeClientAuditHeader.Require(request.Headers, client.DefaultRequestHeaders, correlation));
    }

    /// <summary>Ordinary B2 and token sends accept absence only; no fallback GUID is invented.</summary>
    [Fact]
    public void Given_NoBrowserReservation_When_NativeSenderRuns_Then_HeaderStaysAbsent()
    {
        using var request = new HttpRequestMessage();
        using var client = new HttpClient();
        NativeClientAuditHeader.Attach(request.Headers, client.DefaultRequestHeaders, null);
        NativeClientAuditHeader.Require(request.Headers, client.DefaultRequestHeaders, null);
        Assert.False(request.Headers.Contains(Header));
        Assert.Throws<InvalidOperationException>(() => NativeClientAuditHeader.Attach(request.Headers, client.DefaultRequestHeaders, Guid.Empty));
        request.Headers.Add(Header, Canonical);
        Assert.Throws<InvalidOperationException>(() => NativeClientAuditHeader.Require(request.Headers, client.DefaultRequestHeaders, null));
    }
}
