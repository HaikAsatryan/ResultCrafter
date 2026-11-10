using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore;

// Writes every problem ResultCrafter built (it carries the x-rc marker), whatever the Accept header and whatever
// the order of AddControllers() and AddResultCrafter(). The framework tries writers in registration order. MVC's
// writer runs the CustomizeProblemDetails hook on a copy that has no markers yet, so they leak and Retry-After is
// lost, and it writes nothing for a controller without [ApiController]. A non-JSON Accept header makes the
// default writer refuse, and the fallback then skips the hook. AddResultCrafter() inserts this writer first.
internal sealed class ResultCrafterProblemDetailsWriter(
    IOptions<ProblemDetailsOptions> problemDetailsOptions,
    IOptions<JsonOptions> jsonOptions) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context)
    {
        return context.ProblemDetails.Extensions.TryGetValue(ProblemDetailsKeys.RcMarker, out var marker) &&
               marker is true;
    }

    // Same steps as the framework's default writer, minus its Accept check: fill the defaults, run the hook (the
    // app's and ours), then write the body as application/problem+json.
    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        ApplyFrameworkDefaults(problem);

        problemDetailsOptions.Value.CustomizeProblemDetails?.Invoke(context);

        var typeInfo = jsonOptions.Value.SerializerOptions.GetTypeInfo(problem.GetType());

        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(problem, typeInfo,
            "application/problem+json", context.HttpContext.RequestAborted));
    }

    // The 500 handler and binding failures outside the catalog (413, 415, ...) arrive without a type or title.
    // TypedResults.Problem applies the framework's defaults for a status, so reading them there keeps this writer
    // in step with the framework's own writer instead of copying its table. Every marked problem already carries
    // its status. The early return skips that allocation for Result errors, which arrive complete.
    private static void ApplyFrameworkDefaults(Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        if (problem.Type is not null && problem.Title is not null) return;

        var defaults = TypedResults.Problem(statusCode: problem.Status).ProblemDetails;
        problem.Type ??= defaults.Type;
        problem.Title ??= defaults.Title;
    }
}
