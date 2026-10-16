using System.Reflection;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks explicit opt-in composition and closed admission across the complete independently inventoried native surface.</summary>
public sealed class StorageRegistrationTests
{
    private const string Connection = "Host=localhost;Database=unused_registration_probe";
    private const string FamilySeparator = ":";
    private const string PruneMethod = "PruneAsync";
    private const string TaskConversion = "AsTask";
    private const string OwnershipFailure = "Native protocol storage ownership is unavailable.";
    private static readonly (string Family, Type Contract, Type Adapter)[] Stores =
    [
        ("Application", typeof(IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication>), typeof(OwnedApplicationStore)),
        ("Authorization", typeof(IOpenIddictAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization>), typeof(OwnedAuthorizationStore)),
        ("Scope", typeof(IOpenIddictScopeStore<OpenIddictEntityFrameworkCoreScope>), typeof(OwnedScopeStore)),
        ("Token", typeof(IOpenIddictTokenStore<OpenIddictEntityFrameworkCoreToken>), typeof(OwnedTokenStore))
    ];

    /// <summary>Default production composition stays native while explicit opt-in replaces exactly all four stores.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeComposition_When_OwnershipIsSelected_Then_OnlyExplicitOptInUsesAdapters(bool enabled)
    {
        var services = new ServiceCollection().AddLogging().AddIdentityPersistence(DatabaseProvider.PostgreSql, Connection);
        if (enabled)
        {
            services.AddProtocolStorageOwnership();
        }
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        foreach (var (_, contract, adapter) in Stores)
        {
            var actual = scope.ServiceProvider.GetRequiredService(contract);
            Assert.Equal(enabled ? adapter : adapter.BaseType, actual.GetType());
        }
        Assert.Equal(enabled, scope.ServiceProvider.GetService<ProtocolStorageOwner>() is not null);
    }

    /// <summary>Every database entry rejects an unactivated owner before any native validation, query or write can execute.</summary>
    [Fact]
    public async Task Given_UnactivatedOwner_When_AnyInventoriedEntryIsCalled_Then_NoNativeWorkIsAdmitted()
    {
        var services = new ServiceCollection().AddLogging().AddIdentityPersistence(DatabaseProvider.PostgreSql, Connection).AddProtocolStorageOwnership();
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        foreach (var (family, contract, adapter) in Stores)
        {
            var store = scope.ServiceProvider.GetRequiredService(contract);
            foreach (var method in adapter.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Assert.Contains(StorageInventory.Required, key => key.StartsWith(family + FamilySeparator + method.Name + '`', StringComparison.Ordinal));
                if (method.Name == PruneMethod)
                {
                    await Assert.ThrowsAsync<NotSupportedException>(() => InvokeAsync(store, method));
                }
                else
                {
                    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeAsync(store, method));
                    Assert.Equal(OwnershipFailure, error.Message);
                }
            }
        }
    }

    /// <summary>Exercises actual virtual entrypoints, awaiting boxed ValueTasks and unwrapping only reflection's invocation envelope.</summary>
    private static async Task InvokeAsync(object store, MethodInfo method)
    {
        if (method.IsGenericMethodDefinition)
        {
            method = method.MakeGenericMethod(method.GetGenericArguments().Select(_ => typeof(string)).ToArray());
        }
        var arguments = method.GetParameters().Select(parameter => parameter.ParameterType == typeof(CancellationToken)
            ? (object)TestContext.Current.CancellationToken : parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null).ToArray();
        object? result;
        try
        {
            result = method.Invoke(store, arguments);
        }
        catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException!).Throw(); throw; }
        if (result is ValueTask task)
        {
            await task;
        }
        else if (result is not null && result.GetType().GetMethod(TaskConversion) is { } convert)
        {
            await (Task)convert.Invoke(result, null)!;
        }
    }
}
