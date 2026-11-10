using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     The catch-all <see cref="IExceptionHandler" /> that <c>AddResultCrafter()</c> registers. It logs the
///     exception at Error and writes a 500 ProblemDetails, or the status the app's
///     <c>ExceptionHandlerOptions.StatusCodeSelector</c> chose. Exception details are included according to
///     <see cref="ResultCrafterOptions.ExceptionDetailMode" />.
/// </summary>
/// <remarks>
///     It handles every exception that reaches it, so register your own handlers before <c>AddResultCrafter()</c>.
///     The type is public so other handlers can be ordered around it, as <c>AddResultCrafterEfCore()</c> does.
/// </remarks>
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
                httpContext.Request.Method,
                httpContext.Request.Path.Value ?? string.Empty);

            return false;
        }

        var opt = options.Value;

        var includeDetails = opt.ExceptionDetailMode switch
        {
            ExceptionDetailMode.IncludeExceptionDetails => true,
            ExceptionDetailMode.Sanitized => false,
            _ => _includeDetailsByDefault
        };

        // The exception-handler middleware already set the status: 500, or what the app's
        // ExceptionHandlerOptions.StatusCodeSelector chose. Body and transport must agree.
        var status = httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
            ? httpContext.Response.StatusCode
            : StatusCodes.Status500InternalServerError;
        httpContext.Response.StatusCode = status;

        // The house title for a 500; any other status gets the catalog title (4xx) or the framework one.
        var title = status == StatusCodes.Status500InternalServerError ? "internal_server_error" : null;

        // Log before writing the response so the entry appears even if TryWriteAsync fails.
        ResultCrafterLogger.LogUnhandledException(
            logger,
            exception,
            status,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty,
            ProblemDetailsEnricher.GetInstance(httpContext),
            ProblemDetailsEnricher.GetTraceId(httpContext),
            httpContext.TraceIdentifier);

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
    ///     Determines at startup whether this environment should expose exception details.
    /// </summary>
    private static bool ComputeIsNonProd(IHostEnvironment env)
    {
        // IsStaging() covers the standard "Staging" name, which the substring list below
        // does not ("Staging" does not contain "stage").
        if (env.IsDevelopment() || env.IsStaging()) return true;

        var name = env.EnvironmentName;
        return name.Contains("dev", StringComparison.OrdinalIgnoreCase)
               || name.Contains("local", StringComparison.OrdinalIgnoreCase)
               || name.Contains("test", StringComparison.OrdinalIgnoreCase)
               || name.Contains("qa", StringComparison.OrdinalIgnoreCase)
               || name.Contains("stage", StringComparison.OrdinalIgnoreCase)
               || name.Contains("uat", StringComparison.OrdinalIgnoreCase)
               || name.Contains("preprod", StringComparison.OrdinalIgnoreCase)
               || name.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
               || name.Contains("debug", StringComparison.OrdinalIgnoreCase);
    }
}
