using System.Reflection;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Detects missing, hidden or newly introduced native database entries using a source-reviewed complete API partition.</summary>
public sealed class StorageInventoryTests
{
    /// <summary>Requires every database entry to be a true override and every pure native method to remain unchanged.</summary>
    [Theory]
    [InlineData("Application", typeof(OwnedApplicationStore))]
    [InlineData("Authorization", typeof(OwnedAuthorizationStore))]
    [InlineData("Scope", typeof(OwnedScopeStore))]
    [InlineData("Token", typeof(OwnedTokenStore))]
    public void Given_PinnedNativeSurface_When_AdapterIsInspected_Then_AllDatabaseEntriesAreOverridden(string family, Type adapter)
    {
        const string FamilySeparator = ":";
        var entries = NativeMethods(adapter).ToArray();
        var expected = StorageInventory.Required.Concat(StorageInventory.Pure).Where(key => key.StartsWith(family + FamilySeparator, StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(expected, entries.Select(method => family + FamilySeparator + Key(method)).Order().ToArray());
        foreach (var method in entries)
        {
            var implementation = adapter.GetMethod(method.Name, method.GetGenericArguments().Length, method.GetParameters().Select(p => p.ParameterType).ToArray());
            if (method.IsGenericMethodDefinition)
            {
                implementation = adapter.GetMethods().Single(candidate => Key(candidate) == Key(method));
            }
            Assert.NotNull(implementation);
            if (StorageInventory.Required.Contains(family + FamilySeparator + Key(method)))
            {
                Assert.True(Guards(adapter, implementation, method));
            }
            else
            {
                Assert.NotEqual(adapter, implementation.DeclaringType);
            }
        }
    }

    /// <summary>Proves that omission and hiding cannot satisfy the inventory oracle by name alone.</summary>
    [Fact]
    public void Given_MissingOrHiddenRelationshipGetter_When_InventoryChecks_Then_BothMutantsAreRejected()
    {
        const string DeferredReturn = ":IAsyncEnumerable<";
        var native = typeof(OpenIddictEntityFrameworkCoreTokenStore).GetMethod(nameof(OpenIddictEntityFrameworkCoreTokenStore.GetApplicationIdAsync))!;
        var missing = typeof(MissingGetter).GetMethod(native.Name)!;
        var hidden = typeof(HiddenGetter).GetMethod(native.Name)!;
        Assert.False(Guards(typeof(MissingGetter), missing, native));
        Assert.False(Guards(typeof(HiddenGetter), hidden, native));
        Assert.Equal(65, StorageInventory.Required.Length);
        Assert.Equal(88, StorageInventory.Pure.Length);
        Assert.Equal(19, StorageInventory.Required.Count(key => key.Contains(DeferredReturn, StringComparison.Ordinal)));
    }

    /// <summary>Enumerates the inherited generic native surface while excluding object methods.</summary>
    private static IEnumerable<MethodInfo> NativeMethods(Type adapter) => adapter.BaseType!.GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Where(method => method.IsVirtual && method.DeclaringType != typeof(object));

    /// <summary>Requires the exact base slot, not a new same-named method or inherited unguarded implementation.</summary>
    private static bool Guards(Type adapter, MethodInfo implementation, MethodInfo native) =>
        implementation.DeclaringType == adapter && implementation.GetBaseDefinition() == native.GetBaseDefinition();

    /// <summary>Normalizes reflection signatures without relying on declaration order or nullable-reference metadata.</summary>
    private static string Key(MethodInfo method) => $"{method.Name}`{method.GetGenericArguments().Length}({string.Join(',', method.GetParameters().Select(p => Name(p.ParameterType)))}):{Name(method.ReturnType)}";

    /// <summary>Uses native source type spellings for generic parameter and exact overload comparison.</summary>
    private static string Name(Type type)
    {
        const string ArraySuffix = "[]", GenericOpen = "<", GenericClose = ">";
        const string StringName = "string", LongName = "long", IntName = "int", BooleanName = "bool";
        if (type == typeof(string))
        {
            return StringName;
        }
        if (type == typeof(long))
        {
            return LongName;
        }
        if (type == typeof(int))
        {
            return IntName;
        }
        if (type == typeof(bool))
        {
            return BooleanName;
        }
        if (type.IsArray)
        {
            return Name(type.GetElementType()!) + ArraySuffix;
        }
        return type.IsGenericType ? type.Name.Split('`')[0] + GenericOpen + string.Join(',', type.GetGenericArguments().Select(Name)) + GenericClose : type.Name;
    }

    /// <summary>Seeded omission retains native behavior and must fail the slot-ownership test.</summary>
    private sealed class MissingGetter(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
        IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options) : OpenIddictEntityFrameworkCoreTokenStore(cache, context, options);

    /// <summary>Seeded hiding resembles an override to a name-only test but cannot intercept native virtual dispatch.</summary>
    private sealed class HiddenGetter(IMemoryCache cache, IOpenIddictEntityFrameworkCoreContext context,
        IOptionsMonitor<OpenIddictEntityFrameworkCoreOptions> options) : OpenIddictEntityFrameworkCoreTokenStore(cache, context, options)
    {
        /// <summary>Intentionally hides the native slot for the negative inventory control.</summary>
        public new ValueTask<string?> GetApplicationIdAsync(OpenIddictEntityFrameworkCoreToken token, CancellationToken cancellationToken) => base.GetApplicationIdAsync(token, cancellationToken);
    }
}
