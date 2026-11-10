using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///     Populates RFC 9457 ProblemDetails members and custom extension members on every <c>ProblemDetails</c> response:
///     <c>instance</c> (a URI reference for the request target by default), plus <c>requestId</c> and <c>traceId</c>.
/// </summary>
/// <remarks>
///     Computed values are cached in <see cref="HttpContext.Items" /> using private typed
///     object keys to avoid string-key collisions and to speed up repeated lookups within
///     the same request.
/// </remarks>
internal static class ProblemDetailsEnricher
{
    // Typed object keys avoid string-key collisions in HttpContext.Items and are
    // marginally faster for dictionary lookup (reference equality vs. string comparison).
    private static readonly object InstanceItemKey = new();
    private static readonly object TraceIdItemKey = new();

    /// <summary>The extension member name for the trace id.</summary>
    public const string TraceIdKey = "traceId";

    /// <summary>
    ///     Returns the W3C Trace Context activity ID for <paramref name="ctx" />,
    ///     or an empty string when no activity is present.
    ///     The value is cached in <see cref="HttpContext.Items" /> after the first call.
    /// </summary>
    public static string GetTraceId(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue(TraceIdItemKey, out var cached)) return (string)cached!;

        var value = ctx.Features.Get<IHttpActivityFeature>()
            ?.Activity.Id ?? string.Empty;
        ctx.Items[TraceIdItemKey] = value;
        return value;
    }

    /// <summary>
    ///     Returns a URI reference for the request target that produced the error, using
    ///     <c>PathBase + Path</c> by default (e.g. <c>/api/items/42</c>).
    ///     This is a valid RFC 9457 <c>instance</c> value (relative URI with full path).
    ///     The value is cached in <see cref="HttpContext.Items" /> after the first call.
    /// </summary>
    /// <remarks>
    ///     The HTTP method is intentionally omitted here; it is already available as a
    ///     separate structured-log property and can also be added as a custom ProblemDetails
    ///     extension member if desired.
    /// </remarks>
    public static string GetInstance(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue(InstanceItemKey, out var cached)) return (string)cached!;

        // ToUriComponent escapes what a URI cannot hold (a space becomes %20); Value is the decoded path.
        var value = ctx.Request.PathBase.Add(ctx.Request.Path).ToUriComponent();
        if (string.IsNullOrEmpty(value)) value = "/";

        ctx.Items[InstanceItemKey] = value;
        return value;
    }

    /// <summary>
    ///     Enriches <paramref name="pd" /> with <c>instance</c>, <c>requestId</c>, and
    ///     <c>traceId</c> fields derived from <paramref name="ctx" />.
    ///     Existing values are not overwritten.
    /// </summary>
    public static void Enrich(Microsoft.AspNetCore.Mvc.ProblemDetails pd, HttpContext ctx)
    {
        pd.Instance ??= GetInstance(ctx);
        pd.Extensions.TryAdd("requestId", ctx.TraceIdentifier);
        pd.Extensions.TryAdd(TraceIdKey, GetTraceId(ctx));
    }
}
