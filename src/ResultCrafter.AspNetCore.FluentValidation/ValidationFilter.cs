using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResultCrafter.FluentValidation;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ResultCrafter.AspNetCore.FluentValidation;

/// <summary>
///     A Minimal API endpoint filter that validates the <typeparamref name="TRequest" /> argument
///     with its registered <see cref="IValidator{T}" /> before the endpoint handler runs,
///     short-circuiting with an RFC 9457 ProblemDetails <c>400</c> when validation fails.
/// </summary>
/// <remarks>
///     The filter is a transparent pass-through (the handler runs normally) when the endpoint has
///     no <typeparamref name="TRequest" /> argument, which makes it safe to apply to a whole route
///     group whose endpoints don't all take a <typeparamref name="TRequest" />. An argument bound
///     as <see langword="null" /> (e.g. an optional body that was omitted) is indistinguishable
///     from a missing argument at the filter level and also passes through. If a non-null
///     <typeparamref name="TRequest" /> argument <i>is</i> present but no <see cref="IValidator{T}" />
///     is registered, the filter throws an <see cref="InvalidOperationException" /> so the
///     misconfiguration surfaces immediately instead of silently skipping validation.
///     When parameter binding already failed (the response status is 400 on entry), validation is skipped so the
///     binding error reaches the client unchanged.
///     On failure, the field-error keys are aligned to the application's configured JSON
///     property-naming policy (camelCase by default for Minimal APIs) so they match the property
///     names on the wire, including nested (<c>address.street</c>) and indexed (<c>items[0].name</c>)
///     paths. Apply it via
///     <see cref="ValidationFilterExtensions.WithValidation{TRequest}(Microsoft.AspNetCore.Builder.RouteHandlerBuilder)" />
///     .
/// </remarks>
/// <typeparam name="TRequest">The request type to validate.</typeparam>
public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Filters also run after parameter binding failed (in Production, where binding failures do not
        // throw). The status is already 400 and the handler will not run. Validating now would waste a call,
        // or replace the binding error with the validator's field errors.
        if (context.HttpContext.Response.StatusCode == StatusCodes.Status400BadRequest) return await next(context);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            // No non-null TRequest argument to validate: either the endpoint doesn't take one
            // (expected when the filter is applied to a route group) or an optional argument was
            // bound as null. The two cases are indistinguishable here, so pass through.
            return await next(context);

        // A TRequest argument is present, so a validator is genuinely expected. Fail fast on a
        // missing registration rather than silently skipping validation and letting invalid input
        // reach the handler.
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>()
                        ?? throw new InvalidOperationException(
                            $"WithValidation<{typeof(TRequest).Name}>() is applied to this endpoint, but no " +
                            $"IValidator<{typeof(TRequest).Name}> is registered. Register it via " +
                            "AddResultCrafterValidation() or FluentValidation's AddValidatorsFromAssembly(...), " +
                            "or remove the filter.");

        var error = await validator.ValidateToResultAsync(request, context.HttpContext.RequestAborted);
        if (error is null) return await next(context);

        return AlignKeysToJsonPolicy(error, context.HttpContext).ToProblemResult();
    }

    /// <summary>
    ///     Re-keys the validation field errors using the application's configured JSON
    ///     <see cref="JsonNamingPolicy" /> so the response <c>errors</c> keys match the wire JSON.
    ///     Returns the error unchanged when there is no field-error dictionary or no naming policy
    ///     (a <see langword="null" /> policy means CLR/PascalCase names are used verbatim).
    /// </summary>
    private static Error AlignKeysToJsonPolicy(Error error, HttpContext httpContext)
    {
        if (error.Errors is null) return error;

        var jsonOptions = httpContext.RequestServices.GetService<IOptions<JsonOptions>>();
        var policy = jsonOptions?.Value.SerializerOptions.PropertyNamingPolicy;
        if (policy is null) return error;

        var aligned = FieldKeyNaming.ConvertKeys(policy, error.Errors, StringComparer.OrdinalIgnoreCase);
        return Error.BadRequest(aligned, error.Detail);
    }
}
