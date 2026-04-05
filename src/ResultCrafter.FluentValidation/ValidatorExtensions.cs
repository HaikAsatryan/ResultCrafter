using FluentValidation;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.FluentValidation;

/// <summary>
///    Extension methods that bridge FluentValidation results to ResultCrafter <see cref="Error" /> values.
/// </summary>
public static class ValidatorExtensions
{
   /// <summary>
   ///    Validates <paramref name="instance" /> and returns <see langword="null" /> on success,
   ///    or an <see cref="Error.BadRequest(Dictionary{string,string[]},string?)" /> with
   ///    field-level errors keyed by FluentValidation's property name on failure.
   /// </summary>
   /// <remarks>
   ///    Property names are used as-is from FluentValidation (e.g. <c>"FirstName"</c>,
   ///    <c>"Address.Street"</c>). Configure FluentValidation's
   ///    <c>ValidatorOptions.Global.PropertyNameResolver</c> in your composition root if
   ///    you need a different casing convention (e.g. camelCase).
   /// </remarks>
   public static async Task<Error?> ValidateToResultAsync<T>(this IValidator<T> validator,
      T instance,
      CancellationToken ct = default)
   {
      var result = await validator.ValidateAsync(instance, ct);
      if (result.IsValid)
      {
         return null;
      }

      var errors = result.Errors
                         .GroupBy(
                            e => string.IsNullOrWhiteSpace(e.PropertyName) ? "_" : e.PropertyName,
                            StringComparer.OrdinalIgnoreCase)
                         .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage)
                                  .Where(m => !string.IsNullOrWhiteSpace(m))
                                  .Distinct(StringComparer.Ordinal)
                                  .ToArray(),
                            StringComparer.OrdinalIgnoreCase);

      return Error.BadRequest(errors);
   }
}