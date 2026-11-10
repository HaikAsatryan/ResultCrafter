using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     Intercepts <see cref="BadHttpRequestException" /> and maps it to an RFC 9457
///     ProblemDetails response carrying the exception's own
///     <see cref="BadHttpRequestException.StatusCode" /> (typically <c>400</c>, or e.g.
///     <c>413</c> for an oversized body) instead of letting it fall through to the
///     catch-all <c>500</c> handler.
/// </summary>
/// <remarks>
///     Minimal API parameter binding throws <see cref="BadHttpRequestException" /> only when
///     <c>RouteHandlerOptions.ThrowOnBadRequest</c> is enabled, which ASP.NET Core does
///     automatically in the Development environment. In Production the option defaults to
///     <see langword="false" />, so a binding failure produces a clean 4xx without throwing.
///     Without this handler the same input is therefore <c>500</c> in Development but <c>400</c>
///     in Production. This handler closes that parity gap by formatting the response through the
///     standard ProblemDetails pipeline, matching what the framework's status-code path emits
///     for the carried status code. It is registered before
///     <see cref="ResultCrafterExceptionHandler" /> so it is consulted first.
/// </remarks>
internal sealed class BadHttpRequestExceptionHandler(ILogger<BadHttpRequestExceptionHandler> logger)
    : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Only client-error (4xx) BadHttpRequestExceptions are mapped here. Anything outside the
        // 4xx range (which the framework never produces for this type) falls through to the
        // catch-all 500 handler so it is logged as an unhandled error rather than silently masked.
        if (exception is not BadHttpRequestException { StatusCode: >= 400 and < 500 } badRequest) return false;

        if (httpContext.Response.HasStarted)
        {
            // The response stream is already open; we cannot write ProblemDetails.
            ResultCrafterLogger.LogResponseAlreadyStarted(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path.Value ?? string.Empty);

            return false;
        }

        // Align the response status with the exception's own status before writing, so the
        // ProblemDetails pipeline resolves the title/type for that code (the same output the
        // status-code path produces in Production). The x-rc marker routes the response through
        // the centralised 4xx logging and is stripped before the body is serialised.
        httpContext.Response.StatusCode = badRequest.StatusCode;

        var pd = CreateProblem(badRequest);
        pd.Status = badRequest.StatusCode;
        pd.Extensions[ProblemDetailsKeys.RcMarker] = true;

        var pds = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        return await pds.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = pd,
            Exception = exception
        });
    }

    // On .NET 11 an unreadable JSON body in Production is answered with a validation problem keyed by the
    // JSON path; ConfigureResultCrafterProblemDetails turns that into the house shape. Building the same
    // problem here gives Development the identical body. On .NET 10 Production answers with a plain 400,
    // so Development keeps the plain 400 too.
    private static Microsoft.AspNetCore.Mvc.ProblemDetails CreateProblem(BadHttpRequestException badRequest)
    {
        if (Environment.Version.Major < 11 ||
            badRequest.StatusCode != StatusCodes.Status400BadRequest ||
            badRequest.InnerException is not JsonException json)
            return new Microsoft.AspNetCore.Mvc.ProblemDetails();

        // Fixed messages, not json.Message: the parser text names CLR types and byte positions.
        var path = json.Path ?? "$";
        return new HttpValidationProblemDetails(new Dictionary<string, string[]>
        {
            [path] = [path == "$"
                ? ConfigureResultCrafterProblemDetails.InvalidJson
                : ConfigureResultCrafterProblemDetails.InvalidValue]
        });
    }
}
