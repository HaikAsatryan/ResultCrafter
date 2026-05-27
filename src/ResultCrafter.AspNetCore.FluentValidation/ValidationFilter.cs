using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResultCrafter.AspNetCore.Endpoints;
using ResultCrafter.Core.Primitives;
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
///     group whose endpoints don't all take a <typeparamref name="TRequest" />. If a
///     <typeparamref name="TRequest" /> argument <i>is</i> present but no <see cref="IValidator{T}" />
///     is registered, the filter throws an <see cref="InvalidOperationException" /> so the
///     misconfiguration surfaces immediately instead of silently skipping validation.
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
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            // The endpoint has no TRequest argument to validate. This is expected when the filter
            // is applied to a route group whose endpoints don't all take a TRequest, so pass through.
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

        return AlignKeysToJsonPolicy(error.Value, context.HttpContext).ToProblemResult();
    }

    /// <summary>
    ///     Re-keys the validation field errors using the application's configured JSON
    ///     <see cref="JsonNamingPolicy" /> so the response <c>errors</c> keys match the wire JSON.
    ///     Returns the error unchanged when there is no field-error dictionary or no naming policy
    ///     (a <see langword="null" /> policy means CLR/PascalCase names are used verbatim).
    /// </summary>
    private static Error AlignKeysToJsonPolicy(Error error, HttpContext httpContext)
    {
        if (error.Errors is null || error.Errors.Count == 0) return error;

        var jsonOptions = httpContext.RequestServices.GetService<IOptions<JsonOptions>>();
        var policy = jsonOptions?.Value.SerializerOptions.PropertyNamingPolicy;
        if (policy is null) return error;

        var aligned = new Dictionary<string, string[]>(error.Errors.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, messages) in error.Errors)
        {
            var converted = ConvertPath(policy, key);

            // Distinct source keys can collapse to the same converted key (rare, e.g. "Foo"/"foo");
            // merge their messages rather than throwing or silently dropping one.
            aligned[converted] = aligned.TryGetValue(converted, out var existing)
                ? [.. existing, .. messages]
                : messages;
        }

        return Error.BadRequest(aligned, error.Detail);
    }

    /// <summary>
    ///     Applies <paramref name="policy" /> to each dot-separated segment of
    ///     <paramref name="propertyName" />, matching how System.Text.Json names nested and indexed
    ///     properties on the wire (e.g. <c>Address.Street</c> -&gt; <c>address.street</c>).
    /// </summary>
    private static string ConvertPath(JsonNamingPolicy policy, string propertyName)
    {
        if (!propertyName.Contains('.')) return policy.ConvertName(propertyName);

        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++) segments[i] = policy.ConvertName(segments[i]);

        return string.Join('.', segments);
    }
}
