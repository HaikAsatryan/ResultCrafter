using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.Mvc;

/// <summary>
///     Documents a <c>400 Bad Request</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.BadRequest</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesBadRequestAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status400BadRequest, "application/problem+json");

/// <summary>
///     Documents a <c>401 Unauthorized</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Unauthorized</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesUnauthorizedAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status401Unauthorized, "application/problem+json");

/// <summary>
///     Documents a <c>403 Forbidden</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Forbidden</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesForbiddenAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status403Forbidden, "application/problem+json");

/// <summary>
///     Documents a <c>404 Not Found</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.NotFound</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesNotFoundAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status404NotFound, "application/problem+json");

/// <summary>
///     Documents a <c>409 Conflict</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Conflict</c> or <c>Error.ConcurrencyConflict</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesConflictAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status409Conflict, "application/problem+json");

/// <summary>
///     Documents a <c>410 Gone</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Gone</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesGoneAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status410Gone, "application/problem+json");

/// <summary>
///     Documents a <c>422 Unprocessable Entity</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.UnprocessableEntity</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesUnprocessableEntityAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status422UnprocessableEntity, "application/problem+json");

/// <summary>
///     Documents a <c>429 Too Many Requests</c> ProblemDetails response (<see cref="ProblemResponse" /> schema) in the OpenAPI document.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.TooManyRequests</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesTooManyRequestsAttribute()
    : ProducesResponseTypeAttribute<ProblemResponse>(StatusCodes.Status429TooManyRequests, "application/problem+json");
