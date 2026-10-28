using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Freezes the original cleanup authentication and every native request input before durable child work begins.</summary>
internal sealed class CleanupAssertionRequest
{
    private readonly OpenIddictServerEvents.ProcessAuthenticationContext authentication;
    private readonly ClientAssertionProof proof;
    private readonly ProtocolStorageRequest request;
    private readonly OpenIddictRequest nativeRequest;
    private readonly string parameters;

    /// <summary>Retains identity and a nonprintable parameter digest rather than treating a mutable request reference as immutable authority.</summary>
    internal CleanupAssertionRequest(OpenIddictServerEvents.ProcessAuthenticationContext authentication, ClientAssertionProof proof)
    {
        this.authentication = authentication;
        this.proof = proof;
        request = ProtocolStorageAmbient.Current?.Request ?? throw ProtocolStorageOwner.Unavailable();
        nativeRequest = authentication.Request;
        parameters = Parameters(nativeRequest);
    }

    /// <summary>Requires the same live original request, complete native client proof, revocation endpoint and strict nondecreasing proof time.</summary>
    internal bool Allows(OpenIddictServerTransaction transaction, long watermark)
    {
        request.RequireCurrent();
        var sample = request.Clock.GetUtcNow().ToUnixTimeSeconds();
        return ReferenceEquals(request, ProtocolStorageAmbient.Current?.Request) && ReferenceEquals(authentication.Transaction, transaction) &&
            ReferenceEquals(nativeRequest, transaction.Request) && transaction.EndpointType == OpenIddictServerEndpointType.Revocation &&
            parameters == Parameters(nativeRequest) && proof.IsCurrent(authentication) && sample >= 0 && watermark >= 0 &&
            watermark - sample <= AdministratorAssertion.ClockSkewSeconds && proof.Assertion.Allows(Math.Max(sample, watermark));
    }

    /// <summary>Latches an invalid claim in both the original request and attempted owner so swallowed failures cannot restore authority.</summary>
    internal void Fail(ProtocolStorageOwner owner)
    {
        request.Fail();
        owner.Fail();
    }

    /// <summary>Detects in-place input substitutions without logging native parameters or credential values.</summary>
    private static string Parameters(OpenIddictRequest value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            using var writer = new Utf8JsonWriter(buffer);
            writer.WriteStartObject();
            foreach (var pair in value.GetParameters().OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();
            return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan));
        }
        finally { buffer.Clear(); }
    }
}
