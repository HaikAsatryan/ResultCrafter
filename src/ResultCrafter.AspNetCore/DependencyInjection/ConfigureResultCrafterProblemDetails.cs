using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.Options;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.DependencyInjection;

/// <summary>
///     Registered as <see cref="IPostConfigureOptions{TOptions}" /> so our customisation
///     runs after any user-supplied <c>AddProblemDetails(o =&gt; …)</c> calls, then chains
///     the callbacks rather than replacing them.
/// </summary>
/// <remarks>
///     Responsibilities:
///     <list type="bullet">
///         <item>
///             Enriches every ProblemDetails response with <c>instance</c>, <c>requestId</c>,
///             and <c>traceId</c> via <see cref="ProblemDetailsEnricher" />.
///         </item>
///         <item>
///             Logs <c>4xx</c> client errors produced by ResultCrafter at the level
///             configured via <see cref="ResultCrafterOptions.ClientErrorLogLevel" />.
///         </item>
///         <item>
///             Strips internal <c>x-rc</c> and <c>x-rc-error-id</c> marker extensions
///             before the response body is serialised to the client.
///         </item>
///     </list>
/// </remarks>
internal sealed class ConfigureResultCrafterProblemDetails(
    ILogger<ConfigureResultCrafterProblemDetails> logger,
    IOptions<ResultCrafterOptions> options)
    : IPostConfigureOptions<ProblemDetailsOptions>
{
    public void PostConfigure(string? name, ProblemDetailsOptions opts)
    {
        // Chain so any user-supplied callbacks still fire first.
        var previous = opts.CustomizeProblemDetails;

        opts.CustomizeProblemDetails = ctx =>
        {
            previous?.Invoke(ctx);

            // ── Enrich every ProblemDetails with instance / requestId / traceId ─
            ProblemDetailsEnricher.Enrich(ctx.ProblemDetails, ctx.HttpContext);

            // ── Normalize framework-default responses to the house style ──────
            // Binding failures (Development, via BadHttpRequestExceptionHandler) and bare status
            // codes (Production, via UseStatusCodePages) flow through here too. Aligning their
            // title/type/detail to the catalog gives every mapped response one shape, in both
            // environments, without disturbing Result-based or application-customized responses.
            NormalizeFrameworkProblem(ctx.ProblemDetails, ctx.HttpContext);

            var status = ctx.ProblemDetails.Status ?? 0;
            var ext = ctx.ProblemDetails.Extensions;

            var isRc = ext.TryGetValue(ProblemDetailsKeys.RcMarker, out var marker) && marker is true;

            // ── Log 4xx client errors produced by ResultCrafter ───────────────
            if (status is >= 400 and < 500 && isRc)
            {
                var level = options.Value.ClientErrorLogLevel;

                // Guard avoids the string allocations inside LogClientError when the
                // configured level is filtered out by the logging infrastructure.
                if (logger.IsEnabled(level))
                {
                    var errorId = ext.TryGetValue(ProblemDetailsKeys.RcErrorId, out var eid)
                        ? eid?.ToString()
                        : null;

                    ResultCrafterLogger.LogClientError(
                        logger,
                        level,
                        status,
                        ctx.ProblemDetails.Title,
                        ctx.ProblemDetails.Detail,
                        errorId ?? string.Empty,
                        ctx.HttpContext.Request.Method,
                        ctx.HttpContext.Request.Path.Value ?? string.Empty,
                        ProblemDetailsEnricher.GetInstance(ctx.HttpContext),
                        ProblemDetailsEnricher.GetTraceId(ctx.HttpContext),
                        ctx.HttpContext.TraceIdentifier);
                }
            }

            // ── Strip internal marker extensions before the response is sent ──
            if (!isRc) return;

            ext.Remove(ProblemDetailsKeys.RcMarker);
            ext.Remove(ProblemDetailsKeys.RcErrorId);
        };
    }

    /// <summary>
    ///     Rewrites a framework-default ProblemDetails to the ResultCrafter house style for any
    ///     catalog-mapped status code. Leaves the response untouched when the status is unmapped or
    ///     when the title has already been set by a Result-based response or the application.
    /// </summary>
    private static void NormalizeFrameworkProblem(Microsoft.AspNetCore.Mvc.ProblemDetails pd, HttpContext httpContext)
    {
        var status = pd.Status ?? httpContext.Response.StatusCode;
        if (!HttpErrorCatalog.TryGetErrorType(status, out var type)) return;

        // Only rewrite a title still at the framework default (e.g. "Bad Request"). A Result-based
        // response or an application customization has set a different title we must preserve.
        if (!string.Equals(pd.Title, ReasonPhrases.GetReasonPhrase(status), StringComparison.Ordinal)) return;

        pd.Title = HttpErrorCatalog.Title(type);
        pd.Type ??= HttpErrorCatalog.TypeUri(type);
        pd.Detail ??= HttpErrorCatalog.DefaultDetail(type);
    }
}
