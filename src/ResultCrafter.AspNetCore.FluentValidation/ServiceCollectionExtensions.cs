using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ResultCrafter.AspNetCore.FluentValidation;

/// <summary>
///     Extension methods for registering the FluentValidation validators used by
///     <see cref="ValidationFilter{TRequest}" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string ScanningWarning =
        "Scans assemblies for validators, which trimming can remove. Under trimming or NativeAOT, register each " +
        "IValidator<T> explicitly.";

    /// <summary>
    ///     Registers all FluentValidation validators, public and internal, found in the given
    ///     <paramref name="assemblies" /> (or the calling assembly when none are supplied) as
    ///     <see cref="ServiceLifetime.Scoped" />, so <c>WithValidation&lt;T&gt;()</c> can resolve them.
    /// </summary>
    /// <remarks>
    ///     This is an opt-in convenience; calling FluentValidation's own <c>AddValidatorsFromAssembly(...)</c>
    ///     directly is equally valid. Nothing is scanned unless this (or an equivalent) method is called. Scoped
    ///     matches FluentValidation and keeps validators that depend on scoped services such as an EF Core
    ///     <c>DbContext</c> from being captured as singletons.
    ///     <para>
    ///         Assembly scanning is not trim-safe, and under NativeAOT on .NET 10 finding the calling assembly
    ///         throws. In a trimmed or NativeAOT app, register each validator explicitly instead, for example
    ///         <c>services.AddScoped&lt;IValidator&lt;CreateOrderRequest&gt;, CreateOrderRequestValidator&gt;()</c>.
    ///     </para>
    /// </remarks>
    /// <param name="services">The service collection to add the validators to.</param>
    /// <param name="assemblies">The assemblies to scan. Defaults to the calling assembly when empty.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [RequiresUnreferencedCode(ScanningWarning)]
    public static IServiceCollection AddResultCrafterValidation(this IServiceCollection services,
        params Assembly[] assemblies)
    {
        return Register(services, ServiceLifetime.Scoped,
            assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()]);
    }

    /// <summary>
    ///     Registers all FluentValidation validators, public and internal, found in the given
    ///     <paramref name="assemblies" /> (or the calling assembly when none are supplied) with the given
    ///     <paramref name="lifetime" />, so <c>WithValidation&lt;T&gt;()</c> can resolve them.
    /// </summary>
    /// <remarks>
    ///     Pass <see cref="ServiceLifetime.Singleton" /> for stateless validators consumed by a singleton. The
    ///     trimming and NativeAOT notes on <see cref="AddResultCrafterValidation(IServiceCollection, Assembly[])" />
    ///     apply here too.
    /// </remarks>
    /// <param name="services">The service collection to add the validators to.</param>
    /// <param name="lifetime">The lifetime to register each validator with.</param>
    /// <param name="assemblies">The assemblies to scan. Defaults to the calling assembly when empty.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [RequiresUnreferencedCode(ScanningWarning)]
    public static IServiceCollection AddResultCrafterValidation(this IServiceCollection services,
        ServiceLifetime lifetime,
        params Assembly[] assemblies)
    {
        return Register(services, lifetime, assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()]);
    }

    // Each public overload reads the calling assembly itself (NoInlining), so it is the app's, not this one.
    private static IServiceCollection Register(IServiceCollection services, ServiceLifetime lifetime,
        Assembly[] assemblies)
    {
        return services.AddValidatorsFromAssemblies(assemblies, lifetime, includeInternalTypes: true);
    }
}
