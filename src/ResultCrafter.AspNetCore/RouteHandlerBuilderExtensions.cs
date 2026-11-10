using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     Convenience extensions for documenting ResultCrafter error responses in OpenAPI.
/// </summary>
/// <remarks>
///     These methods only add OpenAPI metadata: the status code with an <c>application/problem+json</c>
///     body of type <see cref="ProblemResponse" />. They have no effect on routing or on the response.
/// </remarks>
public static class RouteHandlerBuilderExtensions
{
    private const string ProblemContentType = "application/problem+json";

    /// <summary>Documents a <c>400 Bad Request</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesBadRequest(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status400BadRequest, ProblemContentType);
    }

    /// <summary>Documents a <c>401 Unauthorized</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesUnauthorized(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status401Unauthorized, ProblemContentType);
    }

    /// <summary>Documents a <c>403 Forbidden</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesForbidden(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status403Forbidden, ProblemContentType);
    }

    /// <summary>Documents a <c>404 Not Found</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesNotFound(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status404NotFound, ProblemContentType);
    }

    /// <summary>Documents a <c>409 Conflict</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesConflict(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status409Conflict, ProblemContentType);
    }

    /// <summary>Documents a <c>410 Gone</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesGone(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status410Gone, ProblemContentType);
    }

    /// <summary>Documents a <c>422 Unprocessable Entity</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesUnprocessableEntity(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status422UnprocessableEntity, ProblemContentType);
    }

    /// <summary>Documents a <c>429 Too Many Requests</c> ProblemDetails response.</summary>
    public static RouteHandlerBuilder ProducesTooManyRequests(this RouteHandlerBuilder builder)
    {
        return builder.Produces<ProblemResponse>(StatusCodes.Status429TooManyRequests, ProblemContentType);
    }
}
