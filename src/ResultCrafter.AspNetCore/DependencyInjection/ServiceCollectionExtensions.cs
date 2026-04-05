using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.ExceptionHandling;
using ResultCrafter.AspNetCore.Options;

namespace ResultCrafter.AspNetCore.DependencyInjection;

/// <summary>
///    Extension methods for registering ResultCrafter services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
   /// <summary>
   ///    Registers ResultCrafter services:
   ///    <list type="bullet">
   ///       <item>RFC 9457 ProblemDetails with automatic enrichment and 4xx structured logging.</item>
   ///       <item><see cref="ResultCrafterExceptionHandler" /> for unhandled 5xx exceptions.</item>
   ///    </list>
   ///    Call <see cref="ApplicationBuilderExtensions.UseResultCrafter" /> in your middleware
   ///    pipeline after calling this method.
   /// </summary>
   public static IServiceCollection AddResultCrafter(this IServiceCollection services,
      Action<ResultCrafterOptions>? configure = null)
   {
      if (configure is not null)
      {
         services.Configure(configure);
      }

      services.AddProblemDetails();
      services.AddSingleton<IPostConfigureOptions<ProblemDetailsOptions>,
         ConfigureResultCrafterProblemDetails>();
      services.AddExceptionHandler<ResultCrafterExceptionHandler>();
      services.AddSingleton<ResultCrafterMarker>();
      services.AddHostedService<ResultCrafterStartupValidator>();

      return services;
   }
}