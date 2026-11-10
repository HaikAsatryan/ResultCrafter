using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore;

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
///             Normalizes framework-default responses (bare status codes, binding failures, unreadable JSON
///             bodies, built-in validation) to the house style.
///         </item>
///         <item>
///             Logs <c>4xx</c> client errors produced by ResultCrafter at the level
///             configured via <see cref="ResultCrafterOptions.ClientErrorLogLevel" />.
///         </item>
///         <item>
///             Moves the internal Retry-After marker to the <c>Retry-After</c> header, and strips the internal
///             <c>x-rc</c> marker extensions before the response body is serialised to the client.
///         </item>
///     </list>
/// </remarks>
internal sealed class ConfigureResultCrafterProblemDetails(
    ILogger<ConfigureResultCrafterProblemDetails> logger,
    IOptions<ResultCrafterOptions> options)
    : IPostConfigureOptions<ProblemDetailsOptions>
{
    // Fixed messages for unreadable JSON bodies: the framework's own text names CLR types and byte
    // positions, which is noise for clients and leaks implementation details.
    internal const string InvalidJson = "invalid_json";
    internal const string InvalidValue = "invalid_value";

    // Every type URI in the framework's ProblemDetailsDefaults table starts with this.
    private const string FrameworkTypeUriPrefix = "https://tools.ietf.org/html/";

    // The title HttpValidationProblemDetails gets unless someone sets another one.
    private static readonly string? FrameworkValidationTitle = new HttpValidationProblemDetails().Title;

    public void PostConfigure(string? name, ProblemDetailsOptions opts)
    {
        // Chain so any user-supplied callbacks still fire first.
        var previous = opts.CustomizeProblemDetails;

        opts.CustomizeProblemDetails = ctx =>
        {
            previous?.Invoke(ctx);

            // ── Enrich every ProblemDetails with instance / requestId / traceId ─
            ProblemDetailsEnricher.Enrich(ctx.ProblemDetails, ctx.HttpContext);
            RemoveTraceIdAlias(ctx.ProblemDetails, ctx.HttpContext);

            // ── Normalize framework-default responses to the house style ──────
            // Binding failures, unreadable JSON bodies, built-in validation and bare status codes flow
            // through here too. Aligning them to the catalog gives every mapped response one shape, in both
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
                    var instance = ProblemDetailsEnricher.GetInstance(ctx.HttpContext);
                    var traceId = ProblemDetailsEnricher.GetTraceId(ctx.HttpContext);

                    ResultCrafterLogger.LogClientError(
                        logger,
                        level,
                        status,
                        ctx.ProblemDetails.Title,
                        ctx.ProblemDetails.Detail,
                        errorId ?? string.Empty,
                        ctx.HttpContext.Request.Method,
                        ctx.HttpContext.Request.Path.Value ?? string.Empty,
                        instance,
                        traceId,
                        ctx.HttpContext.TraceIdentifier);
                }
            }

            // ── Move the Retry-After marker to its header, never into the body ──
            // Runs before the body is written, so the header still goes out.
            if (ext.Remove(ProblemDetailsKeys.RcRetryAfter, out var retryAfter) && retryAfter is string seconds)
                ctx.HttpContext.Response.Headers.RetryAfter = seconds;

            // ── Strip internal marker extensions before the response is sent ──
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

        if (status == StatusCodes.Status400BadRequest &&
            pd is HttpValidationProblemDetails validation &&
            string.Equals(validation.Title, FrameworkValidationTitle, StringComparison.Ordinal))
        {
            NormalizeValidationProblem(validation, httpContext);
            return;
        }

        if (!HttpErrorCatalog.TryGetErrorType(status, out var type)) return;

        // Only rewrite a title still at the framework default. A Result-based response or an
        // application customization has set a different title we must preserve. A null/empty title
        // also counts as "framework default": ASP.NET Core only pre-populates Title (with the reason
        // phrase) for status codes in its ProblemDetailsDefaults table, which has no 429 entry, so
        // without the IsNullOrEmpty branch a bare TooManyRequests response would skip normalization.
        if (!string.IsNullOrEmpty(pd.Title) &&
            !string.Equals(pd.Title, ReasonPhrases.GetReasonPhrase(status), StringComparison.Ordinal))
            return;

        ApplyHouseStyle(pd, type);
    }

    /// <summary>
    ///     A 400 validation problem that still has the framework title. Two framework sources produce one:
    ///     an unreadable JSON body on .NET 11 (every key is a JSON path such as <c>$.count</c>) and built-in
    ///     validation (<c>AddValidation()</c>, keys are C# property paths). An application
    ///     <c>ValidationProblem</c> that kept the default title is treated like built-in validation.
    /// </summary>
    private static void NormalizeValidationProblem(HttpValidationProblemDetails pd, HttpContext httpContext)
    {
        var errors = pd.Errors;
        var jsonPathKeys = errors.Keys.Count(k => k.StartsWith('$'));

        if (jsonPathKeys > 0 && jsonPathKeys == errors.Count)
        {
            var mapped = errors.Keys.ToDictionary(JsonPathToKey, k => k == "$" ? [InvalidJson] : new[] { InvalidValue });
            Replace(errors, mapped);
        }
        else if (jsonPathKeys == 0)
        {
            var policy = NamingPolicy(httpContext);
            if (policy is not null) Replace(errors, FieldKeyNaming.ConvertKeys(policy, errors, StringComparer.Ordinal));
        }
        else
        {
            // Mixed keys (MVC reports a JSON error next to a "request" key): no single rule fits, so leave
            // the framework response as it is.
            return;
        }

        ApplyHouseStyle(pd, ErrorType.BadRequest);
    }

    // On .NET 10 the framework's default writer adds the trace id under the JSON naming policy's name ("trace_id"
    // under snake_case) before this hook runs; .NET 11 writes "traceId". Keep the one "traceId" on both runtimes.
    private static void RemoveTraceIdAlias(Microsoft.AspNetCore.Mvc.ProblemDetails pd, HttpContext httpContext)
    {
        var policy = httpContext.RequestServices?.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            ?.Value.SerializerOptions.PropertyNamingPolicy;
        if (policy is null) return;

        var alias = policy.ConvertName(ProblemDetailsEnricher.TraceIdKey);
        if (!string.Equals(alias, ProblemDetailsEnricher.TraceIdKey, StringComparison.Ordinal))
            pd.Extensions.Remove(alias);
    }

    // "$" is the whole body; "$.address.zip" and "$.items[0].name" name a field by its wire path.
    private static string JsonPathToKey(string path)
    {
        if (path == "$") return "_";
        return path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path[1..];
    }

    // MVC and Minimal APIs keep separate JSON options; use the ones that wrote the request's endpoint.
    private static JsonNamingPolicy? NamingPolicy(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        if (services is null) return null;

        return httpContext.GetEndpoint()?.Metadata.GetMetadata<ActionDescriptor>() is not null
            ? services.GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()?.Value.JsonSerializerOptions
                .PropertyNamingPolicy
            : services.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions
                .PropertyNamingPolicy;
    }

    private static void Replace(IDictionary<string, string[]> target, Dictionary<string, string[]> source)
    {
        target.Clear();
        foreach (var (key, messages) in source) target[key] = messages;
    }

    private static void ApplyHouseStyle(Microsoft.AspNetCore.Mvc.ProblemDetails pd, ErrorType type)
    {
        pd.Title = HttpErrorCatalog.Title(type);
        pd.Detail ??= HttpErrorCatalog.DefaultDetail(type);

        // Replace the framework's own type URI too: its table points 422 at RFC 4918, while the catalog (and so
        // Error.UnprocessableEntity()) uses RFC 9110. A custom type URI from the application is kept.
        if (pd.Type is null || pd.Type.StartsWith(FrameworkTypeUriPrefix, StringComparison.Ordinal))
            pd.Type = HttpErrorCatalog.TypeUri(type);
    }
}
