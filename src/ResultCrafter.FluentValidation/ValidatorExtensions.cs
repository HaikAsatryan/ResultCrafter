using System.Globalization;
using FluentValidation;
using FluentValidation.Results;

namespace ResultCrafter.FluentValidation;

/// <summary>
///     Extension methods that bridge FluentValidation results to ResultCrafter <see cref="Error" /> values.
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>
    ///     Validates <paramref name="instance" /> and returns <see langword="null" /> on success,
    ///     or an <see cref="Error.BadRequest(IReadOnlyDictionary{string,FieldError[]},string?)" /> with
    ///     field-level errors keyed by FluentValidation's property name on failure.
    /// </summary>
    /// <remarks>
    ///     Property names are used as-is from FluentValidation (e.g. <c>"FirstName"</c>,
    ///     <c>"Address.Street"</c>). Configure FluentValidation's
    ///     <c>ValidatorOptions.Global.PropertyNameResolver</c> in your composition root if
    ///     you need a different casing convention (e.g. camelCase).
    ///     Each <see cref="FieldError" /> carries the failure's error code (the rule's <c>WithErrorCode</c> value, or
    ///     FluentValidation's validator name such as <c>NotEmptyValidator</c>) and the rule's limits as args:
    ///     <c>minLength</c>, <c>maxLength</c> (left out when the rule has no maximum, as with
    ///     <c>MinimumLength</c>), <c>from</c>, <c>to</c>, <c>expectedPrecision</c>, <c>expectedScale</c>, and
    ///     <c>comparisonValue</c> when the rule compares with a value that is neither a string nor another property.
    ///     The attempted value and anything measured from it are never copied.
    /// </remarks>
    public static async Task<Error?> ValidateToResultAsync<T>(this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid) return null;

        var errors = result.Errors
            .GroupBy(
                e => string.IsNullOrWhiteSpace(e.PropertyName) ? "_" : e.PropertyName,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Where(e => !string.IsNullOrWhiteSpace(e.ErrorMessage))
                    .DistinctBy(e => e.ErrorMessage, StringComparer.Ordinal)
                    .Select(ToFieldError)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return Error.BadRequest(errors);
    }

    private static FieldError ToFieldError(ValidationFailure failure)
    {
        // A failure added by hand (context.AddFailure) has no code; FluentValidation fills one for every rule.
        var code = string.IsNullOrWhiteSpace(failure.ErrorCode) ? null : failure.ErrorCode;
        var fieldError = new FieldError(failure.ErrorMessage, code);

        var placeholders = failure.FormattedMessagePlaceholderValues;
        if (placeholders is null) return fieldError;

        foreach (var (placeholder, value) in placeholders)
        {
            if (value is null || ArgName(placeholder) is not { } name) continue;
            if (placeholder == "ComparisonValue" && !IsFixedComparison(placeholders, value)) continue;
            // MinimumLength stores "no maximum" as MaxLength -1. It is not a limit, and a client would show "-1".
            if (placeholder == "MaxLength" && value is -1) continue;

            fieldError = WithArg(fieldError, name, value);
        }

        return fieldError;
    }

    // Only the limits a message needs, under fixed camelCase names. Everything else can carry the caller's input or
    // English display text: PropertyValue is the attempted value, TotalLength, Digits and ActualScale are measured
    // from it, and PropertyName, PropertyPath and ComparisonProperty are display names. A custom placeholder is
    // dropped too, because nothing tells whether it holds input.
    private static string? ArgName(string placeholder)
    {
        return placeholder switch
        {
            "MinLength" => "minLength",
            "MaxLength" => "maxLength",
            "From" => "from",
            "To" => "to",
            "ExpectedPrecision" => "expectedPrecision",
            "ExpectedScale" => "expectedScale",
            "ComparisonValue" => "comparisonValue",
            _ => null
        };
    }

    // Compared with another property (ComparisonProperty set), the value is that property's input, such as the
    // password a confirmation must equal. A lambda that is not a plain property (x => x.Password.Trim()) has no
    // ComparisonProperty, so a string value is dropped as well.
    private static bool IsFixedComparison(Dictionary<string, object> placeholders, object value)
    {
        return value is not string &&
               (!placeholders.TryGetValue("ComparisonProperty", out var property) || property is null or "");
    }

    // FieldError takes only the value types ResultCrafter can write as JSON without reflection; others are dropped.
    private static FieldError WithArg(FieldError fieldError, string name, object value)
    {
        return value switch
        {
            int number => fieldError.With(name, number),
            long number => fieldError.With(name, number),
            short number => fieldError.With(name, number),
            byte number => fieldError.With(name, number),
            sbyte number => fieldError.With(name, number),
            ushort number => fieldError.With(name, number),
            uint number => fieldError.With(name, (long)number),
            ulong number => fieldError.With(name, (decimal)number),
            double number when double.IsFinite(number) => fieldError.With(name, number),
            float number when float.IsFinite(number) => fieldError.With(name, (double)number),
            decimal number => fieldError.With(name, number),
            string text => fieldError.With(name, text),
            bool flag => fieldError.With(name, flag),
            Guid id => fieldError.With(name, id),
            DateTimeOffset moment => fieldError.With(name, moment),
            DateTime moment => fieldError.With(name, moment),
            DateOnly date => fieldError.With(name, date.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly time => fieldError.With(name, time.ToString("O", CultureInfo.InvariantCulture)),
            Enum member => fieldError.With(name, member.ToString()),
            _ => fieldError
        };
    }
}
