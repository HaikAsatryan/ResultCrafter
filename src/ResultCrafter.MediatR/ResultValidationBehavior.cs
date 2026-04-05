using FluentValidation;
using FluentValidation.Results;
using MediatR;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.MediatR;

/// <summary>
///    MediatR pipeline behavior that automatically runs all registered
///    <see cref="IValidator{TRequest}" /> instances before a handler returning
///    <see cref="Result{T}" /> executes, short-circuiting with a structured
///    <c>400 Bad Request</c> <see cref="Error" /> if validation fails.
/// </summary>
/// <remarks>
///    <para>
///       Validators run <b>sequentially</b>. Parallel execution via <c>Task.WhenAll</c>
///       is faster but unsafe when validators share a non-thread-safe service such as an
///       EF Core <c>DbContext</c>.
///    </para>
///    <para>
///       <b>All</b> failures from all validators are collected before returning, so the
///       caller sees the complete error picture in a single response rather than discovering
///       one field error at a time.
///    </para>
///    <para>
///       Failures whose <c>PropertyName</c> is empty (global rule failures not tied to a
///       specific field) are collected under the synthetic key <c>"_"</c>, matching the
///       ASP.NET Core convention for top-level request errors.
///    </para>
///    <para>
///       When no <see cref="IValidator{TRequest}" /> is registered for
///       <typeparamref name="TRequest" />, the behavior is a zero-overhead pass-through.
///    </para>
/// </remarks>
public sealed class ResultValidationBehavior<TRequest, T>(IEnumerable<IValidator<TRequest>> validators)
   : IPipelineBehavior<TRequest, Result<T>>
   where TRequest : notnull
{
   // Materialize once to avoid multiple enumeration of the injected IEnumerable.
   private readonly IValidator<TRequest>[] _validators =
      validators as IValidator<TRequest>[] ?? validators.ToArray();

   /// <inheritdoc />
   public async Task<Result<T>> Handle(TRequest request,
      RequestHandlerDelegate<Result<T>> next,
      CancellationToken cancellationToken)
   {
      var error = await ValidateAsync(_validators, request, cancellationToken);

      if (error.HasValue)
      {
         return Result<T>.Fail(error.Value);
      }

      return await next(cancellationToken);
   }

   /// <summary>
   ///    Shared aggregation logic used by both behaviors. Internal so tests can call it
   ///    directly without going through a full MediatR pipeline.
   /// </summary>
   internal static async Task<Error?> ValidateAsync(IValidator<TRequest>[] validators,
      TRequest request,
      CancellationToken ct)
   {
      if (validators.Length == 0)
      {
         return null;
      }

      var context = new ValidationContext<TRequest>(request);
      var failures = new List<ValidationFailure>();

      foreach (var validator in validators)
      {
         var result = await validator.ValidateAsync(context, ct);
         if (!result.IsValid)
         {
            failures.AddRange(result.Errors);
         }
      }

      if (failures.Count == 0)
      {
         return null;
      }

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