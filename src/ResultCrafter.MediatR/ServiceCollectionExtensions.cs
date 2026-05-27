using Microsoft.Extensions.DependencyInjection;

namespace ResultCrafter.MediatR;

/// <summary>
///     Extension methods for registering ResultCrafter validation behaviors with MediatR.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the ResultCrafter validation pipeline behavior into the MediatR pipeline.
    ///     <see cref="ResultValidationBehavior{TRequest,TResponse}" /> runs every registered
    ///     <c>IValidator&lt;TRequest&gt;</c> before each handler whose response is a ResultCrafter
    ///     <c>Result</c> or <c>Result&lt;T&gt;</c>, short-circuiting with a structured <c>400</c> on
    ///     failure. It is registered as a single open behavior; handlers with no registered
    ///     <c>IValidator&lt;TRequest&gt;</c>, or whose response is not a Result, are a transparent
    ///     pass-through.
    /// </summary>
    /// <example>
    ///     <code>
    /// builder.Services.AddMediatR(cfg =>
    /// {
    ///     cfg.RegisterServicesFromAssemblyContaining&lt;Program&gt;();
    ///     cfg.AddResultCrafterValidation();
    /// });
    /// 
    /// // Register your validators (any lifetime; Singleton is fine for stateless validators)
    /// builder.Services.AddValidatorsFromAssemblyContaining&lt;Program&gt;();
    /// </code>
    /// </example>
    public static MediatRServiceConfiguration AddResultCrafterValidation(this MediatRServiceConfiguration cfg)
    {
        cfg.AddOpenBehavior(typeof(ResultValidationBehavior<,>));
        return cfg;
    }
}
