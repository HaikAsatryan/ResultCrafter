using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ResultCrafter.AspNetCore.Endpoints;

/// <summary>
///     Convenience extensions for documenting ResultCrafter error responses in OpenAPI.
/// </summary>
/// <remarks>
///     These methods are purely documentation helpers — they call <c>ProducesProblem</c> to
///     add the corresponding status code to the OpenAPI schema and have no effect on runtime
///     routing or response behaviour.
/// </remarks>
public static class RouteHandlerBuilderExtensions
{
    /// <summary>Documents a <c>400 Bad Request</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesBadRequest(this RouteHandlerBuilder builder)
    {
        return builder.ProducesProblem(StatusCodes.Status400BadRequest);
    }

    /// <summary>Documents a <c>401 Unauthorized</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesUnauthorized(this RouteHandlerBuilder builder)
    {
        return builder.ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    /// <summary>Documents a <c>403 Forbidden</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesForbidden(this RouteHandlerBuilder builder)
    {
        return builder.ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>Documents a <c>404 Not Found</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesNotFound(this RouteHandlerBuilder builder)
    {
        return builder.ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>Documents a <c>409 Conflict</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesConflict(this RouteHandlerBuilder builder)
    {
        return builder.ProducesProblem(StatusCodes.Status409Conflict);
    }
}
