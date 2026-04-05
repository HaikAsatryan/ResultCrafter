using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ResultCrafter.AspNetCore.DependencyInjection;

/// <summary>
///    Extension methods for configuring the ResultCrafter middleware pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
   /// <summary>
   ///    Registers the ResultCrafter middleware pipeline:
   ///    <list type="bullet">
   ///       <item>
   ///          <c>UseExceptionHandler</c> — catches unhandled exceptions and maps them to
   ///          RFC 9457 ProblemDetails via <see cref="ExceptionHandling.ResultCrafterExceptionHandler" />.
   ///       </item>
   ///       <item>
   ///          <c>UseStatusCodePages</c> — converts bare non-success status codes
   ///          (e.g. 404 from routing) to ProblemDetails.
   ///       </item>
   ///    </list>
   ///    Call this <b>before</b> <c>UseRouting</c> and any application middleware.
   ///    Requires <see cref="ServiceCollectionExtensions.AddResultCrafter" /> to have been called first.
   /// </summary>
   public static IApplicationBuilder UseResultCrafter(this IApplicationBuilder app)
   {
      var marker = app.ApplicationServices.GetService<ResultCrafterMarker>()
                   ?? throw new InvalidOperationException(
                      "UseResultCrafter() requires AddResultCrafter() to be called on IServiceCollection first.");

      marker.MiddlewareConfigured = true;

      app.UseExceptionHandler();
      app.UseStatusCodePages();
      return app;
   }
}