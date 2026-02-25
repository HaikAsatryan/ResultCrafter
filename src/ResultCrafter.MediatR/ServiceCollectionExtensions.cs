using Microsoft.Extensions.DependencyInjection;

namespace ResultCrafter.MediatR;

public static class ServiceCollectionExtensions
{
   /// <summary>
   /// Registers the ResultCrafter validation pipeline behaviors into the MediatR pipeline:
   /// <list type="bullet">
   ///   <item>
   ///     <see cref="ResultValidationBehavior{TRequest,T}"/> — for handlers returning
   ///     <c>Result&lt;T&gt;</c>.
   ///   </item>
   ///   <item>
   ///     <see cref="VoidResultValidationBehavior{TRequest}"/> — for handlers returning the
   ///     void <c>Result</c>.
   ///   </item>
   /// </list>
   /// Both behaviors are registered as open generics. MediatR resolves the correct one at
   /// runtime based on each handler's response type. Handlers with no registered
   /// <c>IValidator&lt;TRequest&gt;</c> are a transparent pass-through.
   /// </summary>
   /// <example>
   /// <code>
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
      cfg.AddOpenBehavior(typeof(VoidResultValidationBehavior<>));
      return cfg;
   }
}