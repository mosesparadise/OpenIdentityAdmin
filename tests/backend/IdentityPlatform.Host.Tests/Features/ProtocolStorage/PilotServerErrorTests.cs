using System.Text.Json;
using Microsoft.Net.Http.Headers;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Rejects publication of unowned or contaminated native server errors while preserving safe completed generation failures.</summary>
public sealed class PilotServerErrorTests
{
    private const string Description = "Synthetic native generation failure.";
    private const string ErrorUri = "https://issuer.example.test/errors/generation";
    private const string FullBody = "{\"error\":\"server_error\",\"error_description\":\"Synthetic native generation failure.\",\"error_uri\":\"https://issuer.example.test/errors/generation\"}";
    private const string SyntheticValue = "synthetic-private-output";
    private const string ForeignLocation = "https://foreign.example.test/callback";
    private const string WrongContentType = "text/html";
    private const int NonStringDescription = 42;
    private const string NonStringBody = "{\"error\":\"server_error\",\"error_description\":42}";
    private const string ClientErrorBody = "{\"error\":\"invalid_grant\"}";
    private const string DuplicateErrorBody = "{\"error\":\"server_error\",\"error\":\"server_error\"}";

    /// <summary>Accepts minimal and descriptive native errors only through the same publication guard used before transport copying.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CompletedNativeGenerationFailure_When_ExactSafeJsonIsSealed_Then_PublicationIsAllowed(bool descriptive)
    {
        using var probe = new PilotServerErrorProbe();
        if (descriptive)
        {
            probe.Response.ErrorDescription = Description;
            probe.Response.ErrorUri = ErrorUri;
        }
        await probe.StageAsync(descriptive ? FullBody : PilotServerErrorProbe.Body);
        Assert.True(probe.Exchange.StorageClosed);
        Assert.True(probe.Capture.IsSealed);
        Assert.Null(Record.Exception(probe.Exchange.RequirePublication));
    }

    /// <summary>Each missing ownership receipt independently prevents an otherwise valid sealed native error from reaching transport.</summary>
    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task Given_MissingGenerationOwnershipReceipt_When_ServerErrorWouldPublish_Then_TheGuardRejects(
        bool generation, bool closed, bool recognized, bool sealedOutput)
    {
        using var probe = new PilotServerErrorProbe();
        await probe.StageAsync(generation: generation, closed: closed, recognized: recognized, sealedOutput: sealedOutput);
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }

    /// <summary>Malformed, duplicated, mismatched and credential-bearing output cannot borrow a legitimate native error receipt.</summary>
    [Theory]
    [InlineData("{")]
    [InlineData("{\"error\":\"invalid_grant\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"error\":500}")]
    [InlineData("{\"error\":\"server_error\",\"access_token\":\"synthetic-private-output\"}")]
    [InlineData("{\"error\":\"server_error\",\"id_token\":\"synthetic-private-output\"}")]
    public async Task Given_AlteredStagedErrorBody_When_NativeErrorOwnershipIsComplete_Then_PublicationIsRejected(string body)
    {
        using var probe = new PilotServerErrorProbe();
        await probe.StageAsync(body);
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }

    /// <summary>Equal field counts cannot hide a duplicated error in place of the native description.</summary>
    [Fact]
    public async Task Given_DuplicatedErrorField_When_FieldCountMatchesNativeResponse_Then_PublicationIsRejected()
    {
        using var probe = new PilotServerErrorProbe();
        probe.Response.ErrorDescription = Description;
        await probe.StageAsync(DuplicateErrorBody);
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }

    /// <summary>Native error bytes cannot publish browser authority or an unrelated representation type.</summary>
    [Theory]
    [InlineData("Set-Cookie")]
    [InlineData("Location")]
    [InlineData("Content-Type")]
    public async Task Given_UnsafeErrorHeader_When_ValidNativeErrorIsSealed_Then_PublicationIsRejected(string header)
    {
        using var probe = new PilotServerErrorProbe();
        probe.Capture.Headers[header] = header == HeaderNames.Location ? ForeignLocation :
            header == HeaderNames.ContentType ? WrongContentType : SyntheticValue;
        await probe.StageAsync();
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }

    /// <summary>Even matching native and staged extra fields cannot turn the error path into a credential or metadata channel.</summary>
    [Theory]
    [InlineData(OpenIddictConstants.Parameters.AccessToken)]
    [InlineData(OpenIddictConstants.Parameters.IdToken)]
    [InlineData(OpenIddictConstants.Parameters.Scope)]
    public async Task Given_ContaminatedNativeErrorParameters_When_StagedJsonMatches_Then_PublicationIsRejected(string parameter)
    {
        using var probe = new PilotServerErrorProbe();
        probe.Response.SetParameter(parameter, SyntheticValue);
        await probe.StageAsync(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [OpenIddictConstants.Parameters.Error] = OpenIddictConstants.Errors.ServerError,
            [parameter] = SyntheticValue
        }));
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }

    /// <summary>A nonstring native description cannot be credited merely because the staged JSON reproduces it.</summary>
    [Fact]
    public async Task Given_NonStringNativeErrorDescription_When_StagedJsonMatches_Then_PublicationIsRejected()
    {
        using var probe = new PilotServerErrorProbe();
        probe.Response.SetParameter(OpenIddictConstants.Parameters.ErrorDescription, NonStringDescription);
        await probe.StageAsync(NonStringBody);
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }

    /// <summary>A genuine handled native client error cannot be relabeled as an HTTP server error.</summary>
    [Fact]
    public async Task Given_NativeClientError_When_HttpStatusClaimsServerFailure_Then_PublicationIsRejected()
    {
        using var probe = new PilotServerErrorProbe();
        probe.Response.Error = OpenIddictConstants.Errors.InvalidGrant;
        await probe.StageAsync(ClientErrorBody);
        Assert.Throws<InvalidOperationException>(probe.Exchange.RequirePublication);
    }
}
