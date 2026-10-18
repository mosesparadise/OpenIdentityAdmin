using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Checks fixed client destinations and native discovery capability/type boundaries using actual response contexts.</summary>
public sealed class NativeClientProfileTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    internal const string Issuer = "https://issuer.example/";
    internal const string Client = "https://client.example/";
    internal const string Callback = "https://client.example/callback";
    internal const string KeyId = "client-assertion";
    private const string CredentialSection = "ClientCredentials";

    /// <summary>Names independent endpoint and advertised capability failures that cannot trigger protocol fallback.</summary>
    public enum MetadataFailure
    {
        Issuer, Authorize, Token, Par, Keys, Grant, Response, Mode, Pkce, TokenMethod, ParMethod, UserInfo, MtlsUserInfo
    }

    /// <summary>Every missing capability or substituted endpoint is rejected before credential-bearing requests may be sent.</summary>
    [Theory]
    [InlineData(MetadataFailure.Issuer)]
    [InlineData(MetadataFailure.Authorize)]
    [InlineData(MetadataFailure.Token)]
    [InlineData(MetadataFailure.Par)]
    [InlineData(MetadataFailure.Keys)]
    [InlineData(MetadataFailure.Grant)]
    [InlineData(MetadataFailure.Response)]
    [InlineData(MetadataFailure.Mode)]
    [InlineData(MetadataFailure.Pkce)]
    [InlineData(MetadataFailure.TokenMethod)]
    [InlineData(MetadataFailure.ParMethod)]
    [InlineData(MetadataFailure.UserInfo)]
    [InlineData(MetadataFailure.MtlsUserInfo)]
    public void Given_IncompatibleNativeMetadata_When_Validated_Then_NoCapabilityOrDestinationFallbackIsAccepted(MetadataFailure failure)
    {
        using var credentials = Load(fixture);
        var profile = NativeClientProfile.Create(Issuer, Client, Callback, KeyId, credentials);
        var configuration = Configuration(profile);
        Change(configuration, failure);
        var error = Assert.Throws<InvalidOperationException>(() => NativeClientMetadata.Require(configuration, profile));
        Assert.Equal(ProtocolStorageOwner.Unavailable().Message, error.Message);
    }

    /// <summary>Changes one independently selected native discovery field without altering the pinned local profile.</summary>
    private static void Change(OpenIddictConfiguration configuration, MetadataFailure failure)
    {
        var foreign = new Uri(Client);
        switch (failure)
        {
            case MetadataFailure.Issuer:
                configuration.Issuer = foreign;
                break;
            case MetadataFailure.Authorize:
                configuration.AuthorizationEndpoint = foreign;
                break;
            case MetadataFailure.Token:
                configuration.TokenEndpoint = foreign;
                break;
            case MetadataFailure.Par:
                configuration.PushedAuthorizationEndpoint = null;
                break;
            case MetadataFailure.Keys:
                configuration.JsonWebKeySetUri = foreign;
                break;
            case MetadataFailure.UserInfo:
                configuration.UserInfoEndpoint = foreign;
                break;
            case MetadataFailure.MtlsUserInfo:
                configuration.MtlsUserInfoEndpoint = foreign;
                break;
            default:
                ChangeCapability(configuration, failure);
                break;
        }
    }

    /// <summary>Removes one required capability while leaving every destination valid.</summary>
    private static void ChangeCapability(OpenIddictConfiguration configuration, MetadataFailure failure)
    {
        switch (failure)
        {
            case MetadataFailure.Grant:
                configuration.GrantTypesSupported.Clear();
                break;
            case MetadataFailure.Response:
                configuration.ResponseTypesSupported.Clear();
                break;
            case MetadataFailure.Mode:
                configuration.ResponseModesSupported.Clear();
                break;
            case MetadataFailure.Pkce:
                configuration.CodeChallengeMethodsSupported.Clear();
                break;
            case MetadataFailure.TokenMethod:
                configuration.TokenEndpointAuthMethodsSupported.Clear();
                break;
            case MetadataFailure.ParMethod:
                configuration.PushedAuthorizationEndpointAuthMethodsSupported.Clear();
                break;
        }
    }

    /// <summary>Discovery appends to the configured issuer path without dropping its final path segment.</summary>
    [Theory]
    [InlineData("https://issuer.example/", "https://issuer.example/.well-known/openid-configuration")]
    [InlineData("https://issuer.example/identity", "https://issuer.example/identity/.well-known/openid-configuration")]
    [InlineData("https://issuer.example/identity/", "https://issuer.example/identity/.well-known/openid-configuration")]
    public void Given_CanonicalIssuerPath_When_ProfileIsCreated_Then_DiscoveryPreservesTheExactIssuerPath(string issuer, string discovery)
    {
        using var credentials = Load(fixture);
        var profile = NativeClientProfile.Create(issuer, Client, Callback, KeyId, credentials);
        Assert.Equal(issuer, profile.Issuer.OriginalString);
        Assert.Equal(discovery, profile.ConfigurationEndpoint.AbsoluteUri);
        Assert.Equal("https://issuer.example/connect/par", profile.ParEndpoint.AbsoluteUri);
        Assert.Null(profile.Registration.Key.CreateNativeKey().D);
    }

    /// <summary>Invalid canonical identities, callbacks and key identifiers never expose raw configuration in startup failures.</summary>
    [Theory]
    [InlineData("http://issuer.example/", Client, Callback, KeyId)]
    [InlineData("https://ISSUER.example/", Client, Callback, KeyId)]
    [InlineData(Issuer, "https://client.example/?private=sentinel", Callback, KeyId)]
    [InlineData(Issuer, Client, "https://other.example/callback", KeyId)]
    [InlineData(Issuer, Client, "https://client.example/callback#fragment", KeyId)]
    [InlineData(Issuer, Client, Callback, "")]
    public void Given_InvalidFixedProfile_When_Configured_Then_OnlySafeFailureIsExposed(string issuer, string client, string callback, string identifier)
    {
        using var credentials = Load(fixture);
        var error = Assert.Throws<ClientCredentialFailure>(() => NativeClientProfile.Create(issuer, client, callback, identifier, credentials));
        Assert.Equal(ClientCredentialFailure.SafeMessage, error.Message);
        Assert.Null(error.InnerException);
    }

    /// <summary>Only a correctly typed algorithm array containing the exact asymmetric algorithm passes the actual metadata handler.</summary>
    [Theory]
    [InlineData("[\"RS256\"]", true)]
    [InlineData("[\"HS256\",\"RS256\"]", true)]
    [InlineData("\"RS256\"", false)]
    [InlineData("[\"HS256\"]", false)]
    [InlineData("[\"RS256\",42]", false)]
    [InlineData("null", false)]
    [InlineData("[]", false)]
    public async Task Given_DiscoveryAlgorithmMetadata_When_Parsed_Then_OnlyExplicitCompatibleArraysAreAccepted(string json, bool valid)
    {
        using var credentials = Load(fixture);
        var profile = NativeClientProfile.Create(Issuer, Client, Callback, KeyId, credentials);
        var transaction = new OpenIddictClientTransaction { Response = new() };
        transaction.Response[OpenIddictConstants.Metadata.IdTokenSigningAlgValuesSupported] = JsonSerializer.Deserialize<JsonElement>(json);
        var context = new OpenIddictClientEvents.HandleConfigurationResponseContext(transaction) { Configuration = Configuration(profile) };
        if (valid)
        {
            await new NativeClientMetadata(profile).HandleAsync(context);
            Assert.False(context.IsRejected);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new NativeClientMetadata(profile).HandleAsync(context).AsTask());
        }
    }

    /// <summary>Uses durable synthetic files with one independently owned native client credential set.</summary>
    internal static NativeClientCredentials Load(NativeClientCredentialFixture fixture) => NativeClientCredentials.Load(
        new ConfigurationBuilder().AddInMemoryCollection(fixture.Settings()).Build().GetSection(CredentialSection), DateTimeOffset.UtcNow);

    /// <summary>Provides the explicit native metadata capabilities required by the fixed contract.</summary>
    internal static OpenIddictConfiguration Configuration(NativeClientProfile profile)
    {
        var configuration = new OpenIddictConfiguration
        {
            Issuer = profile.Issuer,
            AuthorizationEndpoint = profile.AuthorizationEndpoint,
            TokenEndpoint = profile.TokenEndpoint,
            PushedAuthorizationEndpoint = profile.ParEndpoint,
            JsonWebKeySetUri = profile.KeysEndpoint
        };
        configuration.GrantTypesSupported.Add(OpenIddictConstants.GrantTypes.AuthorizationCode);
        configuration.ResponseTypesSupported.Add(OpenIddictConstants.ResponseTypes.Code);
        configuration.ResponseModesSupported.Add(OpenIddictConstants.ResponseModes.Query);
        configuration.CodeChallengeMethodsSupported.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
        configuration.TokenEndpointAuthMethodsSupported.Add(OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt);
        configuration.PushedAuthorizationEndpointAuthMethodsSupported.Add(OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt);
        return configuration;
    }
}
