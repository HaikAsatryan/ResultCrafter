using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.AspNetCore.FluentValidation;

/// <summary>
///     Extension methods for attaching <see cref="ValidationFilter{TRequest}" /> to Minimal API
///     endpoints and route groups.
/// </summary>
public static class ValidationFilterExtensions
{
    /// <summary>
    ///     Adds a <see cref="ValidationFilter{TRequest}" /> to the endpoint, validating the
    ///     <typeparamref name="TRequest" /> argument with its registered <c>IValidator&lt;TRequest&gt;</c>
    ///     before the handler runs, and documents the <c>400</c> ProblemDetails response in OpenAPI.
    /// </summary>
    /// <typeparam name="TRequest">The request type to validate.</typeparam>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        return builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesBadRequest();
    }

    /// <summary>
    ///     Adds a <see cref="ValidationFilter{TRequest}" /> to every endpoint in the route group,
    ///     validating the <typeparamref name="TRequest" /> argument with its registered
    ///     <c>IValidator&lt;TRequest&gt;</c> before each handler runs, and documents the <c>400</c>
    ///     ProblemDetails response in OpenAPI for the group.
    /// </summary>
    /// <typeparam name="TRequest">The request type to validate.</typeparam>
    public static RouteGroupBuilder WithValidation<TRequest>(this RouteGroupBuilder builder)
        where TRequest : class
    {
        return builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .WithMetadata(new ProducesResponseTypeMetadata(
                StatusCodes.Status400BadRequest,
                typeof(ProblemResponse),
                ["application/problem+json"]));
    }
}
