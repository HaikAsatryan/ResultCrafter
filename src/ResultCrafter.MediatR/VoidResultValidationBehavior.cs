using FluentValidation;
using MediatR;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.MediatR;

/// <summary>
/// MediatR pipeline behavior that automatically runs all registered
/// <see cref="IValidator{TRequest}"/> instances before a void <see cref="Result"/>
/// handler executes, short-circuiting with a structured <c>400 Bad Request</c>
/// <see cref="Error"/> if validation fails.
/// </summary>
/// <remarks>
/// This type exists alongside <see cref="ResultValidationBehavior{TRequest,T}"/>
/// because <c>IPipelineBehavior</c> requires an exact response-type match — a single
/// open-generic behavior cannot cover both <c>Result</c> and <c>Result&lt;T&gt;</c>
/// without reflection. Both are registered via
/// <see cref="ServiceCollectionExtensions.AddResultCrafterValidation"/>.
/// See <see cref="ResultValidationBehavior{TRequest,T}"/> for full behavioral notes.
/// </remarks>
public sealed class VoidResultValidationBehavior<TRequest>(IEnumerable<IValidator<TRequest>> validators)
   : IPipelineBehavior<TRequest, Result>
   where TRequest : notnull
{
   // Materialize once to avoid multiple enumeration of the injected IEnumerable.
   private readonly IValidator<TRequest>[] _validators =
      validators as IValidator<TRequest>[] ?? validators.ToArray();

   /// <inheritdoc />
   public async Task<Result> Handle(TRequest request,
      RequestHandlerDelegate<Result> next,
      CancellationToken cancellationToken)
   {
      var error = await ResultValidationBehavior<TRequest, object>.ValidateAsync(_validators, request, cancellationToken);

      if (error.HasValue)
      {
         return Result.Fail(error.Value);
      }

      return await next(cancellationToken);
   }
}