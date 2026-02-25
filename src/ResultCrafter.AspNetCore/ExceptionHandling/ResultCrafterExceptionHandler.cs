using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.Options;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.ExceptionHandling;

/// <inheritdoc />
public sealed class ResultCrafterExceptionHandler(
   ILogger<ResultCrafterExceptionHandler> logger,
   IOptions<ResultCrafterOptions> options,
   IHostEnvironment env)
   : IExceptionHandler
{
   // Evaluated once at startup; avoids per-request environment name inspection.
   private readonly bool _includeDetailsByDefault = ComputeIsNonProd(env);

   /// <inheritdoc />
   public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
      Exception exception,
      CancellationToken cancellationToken)
   {
      if (httpContext.Response.HasStarted)
      {
         // The response stream is already open; we cannot write ProblemDetails.
         // Log at warning so the gap is visible without alarming as an unhandled error.
         ResultCrafterLogger.LogResponseAlreadyStarted(
            logger,
            httpMethod: httpContext.Request.Method,
            path: httpContext.Request.Path.Value ?? string.Empty);

         return false;
      }

      var opt = options.Value;

      var includeDetails = opt.ExceptionDetailMode switch
      {
         ExceptionDetailMode.IncludeExceptionDetails => true,
         ExceptionDetailMode.Sanitized => false,
         _ => _includeDetailsByDefault
      };

      const int status = StatusCodes.Status500InternalServerError;
      const string title = "internal_server_error";

      // Log before writing the response so the entry appears even if TryWriteAsync fails.
      ResultCrafterLogger.LogUnhandledException(
         logger,
         exception,
         statusCode: status,
         httpMethod: httpContext.Request.Method,
         path: httpContext.Request.Path.Value ?? string.Empty,
         instance: ProblemDetailsEnricher.GetInstance(httpContext),
         traceId: ProblemDetailsEnricher.GetTraceId(httpContext),
         requestId: httpContext.TraceIdentifier);

      var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
      {
         Status = status,
         Title = title,
         Detail = includeDetails ? exception.ToString() : opt.DefaultServerErrorMessage,
         Extensions =
         {
            [ProblemDetailsKeys.RcMarker] = true
         }
      };

      var pds = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
      return await pds.TryWriteAsync(new ProblemDetailsContext
      {
         HttpContext = httpContext,
         ProblemDetails = pd,
         Exception = exception
      });
   }

   /// <summary>
   /// Determines at startup whether this environment should expose exception details.
   /// The single ToLowerInvariant() allocation happens once and is not on a hot path.
   /// </summary>
   private static bool ComputeIsNonProd(IHostEnvironment env)
   {
      if (env.IsDevelopment())
      {
         return true;
      }

      var name = env.EnvironmentName;
      if (name.Length == 0)
      {
         return false;
      }

      var lower = name.ToLowerInvariant();
      return lower.Contains("dev", StringComparison.Ordinal)
             || lower.Contains("local", StringComparison.Ordinal)
             || lower.Contains("test", StringComparison.Ordinal)
             || lower.Contains("qa", StringComparison.Ordinal)
             || lower.Contains("stage", StringComparison.Ordinal)
             || lower.Contains("uat", StringComparison.Ordinal)
             || lower.Contains("preprod", StringComparison.Ordinal)
             || lower.Contains("sandbox", StringComparison.Ordinal)
             || lower.Contains("debug", StringComparison.Ordinal);
   }
}