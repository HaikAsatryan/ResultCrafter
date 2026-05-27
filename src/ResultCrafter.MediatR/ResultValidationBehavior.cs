using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.MediatR;

/// <summary>
///     MediatR pipeline behavior that automatically runs all registered
///     <see cref="IValidator{TRequest}" /> instances before a handler returning a ResultCrafter
///     <see cref="Result" /> or <see cref="Result{T}" /> executes, short-circuiting with a
///     structured <c>400 Bad Request</c> <see cref="Error" /> if validation fails.
/// </summary>
/// <remarks>
///     <para>
///         Registered once as an open behavior over <c>IPipelineBehavior&lt;TRequest, TResponse&gt;</c>
///         via <see cref="ServiceCollectionExtensions.AddResultCrafterValidation" />. A single
///         fully-open behavior is required because .NET DI cannot register an arity-1 implementation
///         (a behavior that fixes <c>TResponse</c> to the void <see cref="Result" />) against the
///         arity-2 <c>IPipelineBehavior&lt;,&gt;</c> service. It covers both the void
///         <see cref="Result" /> and the generic <see cref="Result{T}" /> shapes, and is a
///         zero-overhead pass-through for any other response type, so it never interferes with
///         handlers outside the Result pattern.
///     </para>
///     <para>
///         Validators run <b>sequentially</b>. Parallel execution via <c>Task.WhenAll</c>
///         is faster but unsafe when validators share a non-thread-safe service such as an
///         EF Core <c>DbContext</c>.
///     </para>
///     <para>
///         <b>All</b> failures from all validators are collected before returning, so the
///         caller sees the complete error picture in a single response rather than discovering
///         one field error at a time.
///     </para>
///     <para>
///         Failures whose <c>PropertyName</c> is empty (global rule failures not tied to a
///         specific field) are collected under the synthetic key <c>"_"</c>, matching the
///         ASP.NET Core convention for top-level request errors.
///     </para>
///     <para>
///         When no <see cref="IValidator{TRequest}" /> is registered for
///         <typeparamref name="TRequest" />, the behavior is a zero-overhead pass-through.
///     </para>
/// </remarks>
public sealed class ResultValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Materialize once to avoid multiple enumeration of the injected IEnumerable.
    private readonly IValidator<TRequest>[] _validators =
        validators as IValidator<TRequest>[] ?? validators.ToArray();

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only Result / Result<T> responses can carry a validation failure; for any other response
        // type, or when no validator is registered, run the handler without added work.
        if (!ResultResponseFactory<TResponse>.IsSupported || _validators.Length == 0)
            return await next(cancellationToken);

        var error = await ValidateAsync(_validators, request, cancellationToken);

        if (error.HasValue) return ResultResponseFactory<TResponse>.Fail(error.Value);

        return await next(cancellationToken);
    }

    /// <summary>
    ///     Shared aggregation logic. Internal so tests can call it directly without going through
    ///     a full MediatR pipeline.
    /// </summary>
    internal static async Task<Error?> ValidateAsync(IValidator<TRequest>[] validators,
        TRequest request,
        CancellationToken ct)
    {
        if (validators.Length == 0) return null;

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, ct);
            if (!result.IsValid) failures.AddRange(result.Errors);
        }

        if (failures.Count == 0) return null;

        var fieldErrors = failures
            .GroupBy(
                f => string.IsNullOrWhiteSpace(f.PropertyName) ? "_" : f.PropertyName,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return Error.BadRequest(fieldErrors);
    }
}

/// <summary>
///     Resolves and caches the factory that produces a failed <typeparamref name="TResponse" />
///     from an <see cref="Error" />, supporting the void <see cref="Result" /> and generic
///     <see cref="Result{T}" /> shapes. The delegate is bound once per closed
///     <typeparamref name="TResponse" /> (static field), so the per-request path is a field read
///     plus a delegate call, never reflection.
/// </summary>
internal static class ResultResponseFactory<TResponse>
{
    private static readonly Func<Error, TResponse>? FailFactory = Build();

    /// <summary>Whether <typeparamref name="TResponse" /> is a <c>Result</c> or <c>Result&lt;T&gt;</c>.</summary>
    public static bool IsSupported => FailFactory is not null;

    /// <summary>Builds the failed <typeparamref name="TResponse" /> carrying <paramref name="error" />.</summary>
    public static TResponse Fail(Error error)
    {
        return FailFactory is null
            ? throw new InvalidOperationException($"{typeof(TResponse)} is not a ResultCrafter Result type.")
            : FailFactory(error);
    }

    private static Func<Error, TResponse>? Build()
    {
        var responseType = typeof(TResponse);

        var isResultType = responseType == typeof(Result)
                           || (responseType.IsGenericType
                               && responseType.GetGenericTypeDefinition() == typeof(Result<>));
        if (!isResultType) return null;

        // Result.Fail(Error) and Result<T>.Fail(Error) are public static methods whose signature is
        // exactly Func<Error, TResponse>, so bind the delegate once here rather than per request.
        var fail = responseType.GetMethod(
            nameof(Result.Fail),
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(Error)],
            null);

        return fail?.CreateDelegate<Func<Error, TResponse>>();
    }
}
