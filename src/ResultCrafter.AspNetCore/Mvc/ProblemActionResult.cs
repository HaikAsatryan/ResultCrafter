using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.Mvc;

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
///         <see cref="ExecuteResultAsync" /> calls <see cref="IProblemDetailsService.WriteAsync" />.
///         <c>AddResultCrafter()</c> registers an <see cref="IProblemDetailsWriter" /> first in line that
///         takes every ResultCrafter problem, so the body is the house shape whatever the order of
///         <c>AddControllers()</c> and <c>AddResultCrafter()</c> and whatever the <c>Accept</c> header. Without
///         <c>AddResultCrafter()</c>, <c>WriteAsync</c> throws <see cref="InvalidOperationException" /> when no
///         writer can handle the context.
///     </para>
/// </remarks>
public sealed class ProblemActionResult : ActionResult, IStatusCodeActionResult
{
    /// <summary>
    ///     Initializes a new instance of <see cref="ProblemActionResult" />.
    /// </summary>
    /// <param name="error">The <see cref="Error" /> to convert into a ProblemDetails response.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <see langword="null" />.</exception>
    public ProblemActionResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>The <see cref="Error" /> this result was constructed from.</summary>
    public Error Error { get; }

    /// <summary>The HTTP status code the error maps to, so MVC filters and middleware can read it.</summary>
    public int? StatusCode => HttpErrorCatalog.Status(Error.Type);

    /// <inheritdoc />
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var httpContext = context.HttpContext;

        if (httpContext.Response.HasStarted) return;

        // Build without enrichment; ConfigureResultCrafterProblemDetails will enrich
        // (instance, traceId, requestId) and strip internal x-rc marker extensions
        // before the body is serialised.
        var pd = ProblemDetailsBuilder.Build(Error);

        // Align the transport status with the mapped error before writing. MVC leaves the
        // response at 200 for a custom ActionResult, and IProblemDetailsService serialises
        // the body but never copies pd.Status onto the response.
        httpContext.Response.StatusCode = pd.Status ?? StatusCodes.Status500InternalServerError;

        var pds = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await pds.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = pd
        });
    }
}
