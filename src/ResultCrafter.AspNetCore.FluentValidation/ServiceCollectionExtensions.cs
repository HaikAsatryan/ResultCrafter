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
    /// <summary>
    ///     Registers all FluentValidation validators discovered in the given
    ///     <paramref name="assemblies" /> (or the calling assembly when none are supplied) so they
    ///     can be resolved by <c>WithValidation&lt;T&gt;()</c>.
    /// </summary>
    /// <remarks>
    ///     This is an opt-in convenience with sensible defaults; calling FluentValidation's own
    ///     <c>AddValidatorsFromAssembly(...)</c> directly is equally valid. Registration is left
    ///     entirely to the consumer: nothing is scanned unless this (or an equivalent) method is
    ///     called, and the <paramref name="lifetime" /> is configurable. The default is
    ///     <see cref="ServiceLifetime.Scoped" /> (as FluentValidation uses), so validators that depend
    ///     on scoped services such as an EF Core <c>DbContext</c> are not captured as singletons; pass
    ///     <see cref="ServiceLifetime.Singleton" /> for stateless validators consumed by a singleton.
    /// </remarks>
    /// <param name="services">The service collection to add the validators to.</param>
    /// <param name="lifetime">
    ///     The lifetime to register each validator with. Defaults to <see cref="ServiceLifetime.Scoped" />,
    ///     matching FluentValidation.
    /// </param>
    /// <param name="assemblies">The assemblies to scan. Defaults to the calling assembly when empty.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddResultCrafterValidation(this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        params Assembly[] assemblies)
    {
        var targets = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];
        return services.AddValidatorsFromAssemblies(targets, lifetime);
    }
}
