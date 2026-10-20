using System.Security.Claims;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves native state observation retains prepared claim and challenge identity through later cleanup checks.</summary>
public sealed class NativeStateBindingTests
{
    /// <summary>Names independently mutated native continuation boundaries.</summary>
    public enum Mutation
    {
        Principal, Nonce, Verifier, Request, Options, Registration, CustomState, StateInclusion, Audience, Resource, ParPolicy, IdentityPartition
    }
    private const string Client = "identity-admin-bff";
    private const string Issuer = "https://issuer.example/";
    private const string Nonce = "prepared-nonce";
    private const string Changed = "changed-value";
    private const string TokenId = "native-created-token-id";
    private const string State = "native-created-reference";

    /// <summary>Only the native token identifier and opaque output are expected additions during the generation window.</summary>
    [Fact]
    public void Given_PreparedChallenge_When_NativeGenerationAddsIdentifierAndState_Then_OriginalBindingRemainsCurrent()
    {
        var context = Create();
        var binding = new NativeStateBinding(context);
        context.StateTokenPrincipal!.SetTokenId(TokenId);
        context.StateToken = State;
        binding.RequireCurrent();
    }

    /// <summary>Rejects data and object substitution even when original dates or fixed registration strings remain unchanged.</summary>
    [Theory]
    [InlineData(Mutation.Principal)]
    [InlineData(Mutation.Nonce)]
    [InlineData(Mutation.Verifier)]
    [InlineData(Mutation.Request)]
    [InlineData(Mutation.Options)]
    [InlineData(Mutation.Registration)]
    [InlineData(Mutation.CustomState)]
    [InlineData(Mutation.StateInclusion)]
    [InlineData(Mutation.Audience)]
    [InlineData(Mutation.Resource)]
    [InlineData(Mutation.ParPolicy)]
    [InlineData(Mutation.IdentityPartition)]
    public void Given_PreparedChallenge_When_ContinuationChanges_Then_BindingRejects(Mutation mutation)
    {
        var context = Create();
        var binding = new NativeStateBinding(context);
        Change(context, mutation);
        Assert.Throws<InvalidOperationException>(binding.RequireCurrent);
    }

    /// <summary>Changes the exact selected continuation boundary without invoking protocol generation.</summary>
    private static void Change(OpenIddictClientEvents.ProcessChallengeContext context, Mutation mutation)
    {
        Action mutate = mutation switch
        {
            Mutation.IdentityPartition => () => Partition(context.StateTokenPrincipal!),
            Mutation.CustomState => () => context.Parameters[OpenIddictConstants.Parameters.State] = Changed,
            Mutation.StateInclusion => () => context.IncludeStateToken = !context.IncludeStateToken,
            Mutation.Audience => () => context.Audiences.Add(Changed),
            Mutation.Resource => () => context.Resources.Add(Changed),
            Mutation.ParPolicy => () => context.Registration.DisablePushedAuthorizationRequests = !context.Registration.DisablePushedAuthorizationRequests,
            Mutation.Principal => () => context.StateTokenPrincipal = context.StateTokenPrincipal!.Clone(),
            Mutation.Nonce => () => context.StateTokenPrincipal!.SetClaim(OpenIddictConstants.Claims.Private.Nonce, Changed),
            Mutation.Verifier => () => context.CodeVerifier = Changed,
            Mutation.Request => () => context.Transaction.Request = new(),
            Mutation.Options => () => context.Transaction.Options = new(),
            Mutation.Registration => () => context.Transaction.Registration = new()
            {
                RegistrationId = Client,
                Issuer = new(Issuer)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        mutate();
    }

    /// <summary>Preserves flattened claims but changes which identity native signing selects.</summary>
    private static void Partition(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        var clone = identity.Clone();
        foreach (var claim in identity.Claims.ToArray())
        {
            identity.RemoveClaim(claim);
        }
        principal.AddIdentity(clone);
    }

    /// <summary>Constructs native context types with bounded synthetic prepared claims and no database/HTTP transport.</summary>
    private static OpenIddictClientEvents.ProcessChallengeContext Create() => new(new OpenIddictClientTransaction
    {
        Options = new(),
        Request = new(),
        Registration = new() { RegistrationId = Client, Issuer = new(Issuer) }
    })
    {
        StateTokenPrincipal = new ClaimsPrincipal(new ClaimsIdentity()).SetClaim(OpenIddictConstants.Claims.Private.Nonce, Nonce)
    };
}
