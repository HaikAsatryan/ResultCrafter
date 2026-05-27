using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore.ProblemDetails;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.AspNetCore.Controllers;

/// <summary>
///     An <see cref="ActionResult" /> that writes an RFC 9457 ProblemDetails response for a
///     given <see cref="Error" />, routing through <see cref="IProblemDetailsService" /> so
///     that ResultCrafter's enrichment (instance, traceId, requestId), 4xx structured logging,
///     and internal marker-extension stripping all fire identically to the Minimal API path.
/// </summary>
/// <remarks>
///     <para>
///         This result is returned by ResultCrafter controller extension methods on the failure
///         path. Application code typically does not instantiate it directly.
///     </para>
///     <para>
///         <see cref="ExecuteResultAsync" /> calls
///         <see cref="IProblemDetailsService.WriteAsync" />, which is guaranteed to succeed
///         because <c>AddResultCrafter()</c> always registers <c>AddProblemDetails()</c> and
///         therefore always provides a capable <see cref="IProblemDetailsWriter" />. If no
///         writer could handle the context, <c>WriteAsync</c> throws
///         <see cref="InvalidOperationException" /> — the same failure mode as any
///         misconfigured ASP.NET Core ProblemDetails setup.
///     </para>
/// </remarks>
public sealed class ProblemActionResult : ActionResult
{
    /// <summary>
    ///     Initializes a new instance of <see cref="ProblemActionResult" />.
    /// </summary>
    /// <param name="error">The <see cref="Error" /> to convert into a ProblemDetails response.</param>
    public ProblemActionResult(Error error)
    {
        Error = error;
    }

    /// <summary>The <see cref="Error" /> this result was constructed from.</summary>
    public Error Error { get; }

    /// <inheritdoc />
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var httpContext = context.HttpContext;

        if (httpContext.Response.HasStarted) return;

        // Build without enrichment; ConfigureResultCrafterProblemDetails will enrich
        // (instance, traceId, requestId) and strip internal x-rc marker extensions
        // before the body is serialised.
        var pd = ProblemDetailsBuilder.Build(Error);

        var pds = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await pds.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = pd
        });
    }
}
