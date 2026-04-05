using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore.ExceptionHandling;

namespace ResultCrafter.AspNetCore.EfCore;

/// <summary>
///    Extension methods for registering the ResultCrafter EF Core integration.
/// </summary>
public static class ServiceCollectionExtensions
{
   /// <summary>
   ///    Registers the ResultCrafter EF Core integration.
   ///    Intercepts <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException" />
   ///    and maps it to a <c>409 ConcurrencyConflict</c> ProblemDetails response.
   ///    Must be called after <see cref="DependencyInjection.ServiceCollectionExtensions.AddResultCrafter" />.
   /// </summary>
   public static IServiceCollection AddResultCrafterEfCore(this IServiceCollection services)
   {
      // Locate the generic 500 handler that AddResultCrafter() registered.
      // We must insert the concurrency handler *before* it so that
      // DbUpdateConcurrencyException is matched as a 409 rather than falling
      // through to the catch-all 500 path.
      var handlerDescriptor =
         services.FirstOrDefault(d => d.ImplementationType == typeof(ResultCrafterExceptionHandler));

      if (handlerDescriptor is null)
      {
         throw new InvalidOperationException(
            "AddResultCrafterEfCore() requires AddResultCrafter() to be called first.");
      }

      var idx = services.IndexOf(handlerDescriptor);

      services.Insert(
         idx,
         ServiceDescriptor.Singleton<IExceptionHandler, EfCoreConcurrencyExceptionHandler>());

      return services;
   }
}