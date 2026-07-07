using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.EfCore;

internal sealed class EfCoreConcurrencyExceptionHandler(ILogger<EfCoreConcurrencyExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException) return false;

        if (httpContext.Response.HasStarted)
        {
            ResultCrafterLogger.LogResponseAlreadyStarted(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path.Value ?? string.Empty);

            return false;
        }

        var error = Error.ConcurrencyConflict();

        var pd = ProblemDetailsBuilder.Build(error);

        // Align the response status with the mapped error before writing. The exception-handler
        // middleware has already set 500, and IProblemDetailsService serialises the body but
        // never copies pd.Status onto the transport response.
        httpContext.Response.StatusCode = HttpErrorCatalog.Status(error.Type);

        var pds = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        return await pds.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = pd,
            Exception = exception
        });
    }
}
