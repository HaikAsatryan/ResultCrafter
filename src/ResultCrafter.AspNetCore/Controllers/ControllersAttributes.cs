using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ResultCrafter.AspNetCore.Controllers;

/// <summary>
///     Documents a <c>400 Bad Request</c> ProblemDetails response in the OpenAPI schema.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.BadRequest</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesBadRequestAttribute()
    : ProducesResponseTypeAttribute<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status400BadRequest);

/// <summary>
///     Documents a <c>401 Unauthorized</c> ProblemDetails response in the OpenAPI schema.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Unauthorized</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesUnauthorizedAttribute()
    : ProducesResponseTypeAttribute<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized);

/// <summary>
///     Documents a <c>403 Forbidden</c> ProblemDetails response in the OpenAPI schema.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Forbidden</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesForbiddenAttribute()
    : ProducesResponseTypeAttribute<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden);

/// <summary>
///     Documents a <c>404 Not Found</c> ProblemDetails response in the OpenAPI schema.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.NotFound</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesNotFoundAttribute()
    : ProducesResponseTypeAttribute<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

/// <summary>
///     Documents a <c>409 Conflict</c> ProblemDetails response in the OpenAPI schema.
///     Apply to controller actions that may return a <see cref="ProblemActionResult" />
///     from <c>Error.Conflict</c> or <c>Error.ConcurrencyConflict</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProducesConflictAttribute()
    : ProducesResponseTypeAttribute<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);
