using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.EfCore;

/// <summary>
///     Maps an optimistic-concurrency <see cref="DbUpdateConcurrencyException" /> to a 409 ProblemDetails.
/// </summary>
/// <remarks>
///     Some providers (Npgsql for EF Core 11) also throw <see cref="DbUpdateConcurrencyException" /> for a
///     serialization failure (SQLSTATE <c>40001</c>) or a deadlock (<c>40P01</c>). Those are server-side
///     failures, not a stale row the caller can refetch, so they are left to the 500 handler.
/// </remarks>
internal sealed partial class EfCoreConcurrencyExceptionHandler(ILogger<EfCoreConcurrencyExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException || IsTransactionFailure(exception)) return false;

        if (httpContext.Response.HasStarted)
        {
            LogResponseAlreadyStarted(
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

    private static bool IsTransactionFailure(Exception exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is DbException { SqlState: "40001" or "40P01" }) return true;
        }

        return false;
    }

    // Same event as the core handlers' "response already started" entry.
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Response already started for {HttpMethod} {Path}; " +
                  "ResultCrafter cannot write ProblemDetails for the unhandled exception.")]
    private static partial void LogResponseAlreadyStarted(ILogger logger, string httpMethod, string path);
}
