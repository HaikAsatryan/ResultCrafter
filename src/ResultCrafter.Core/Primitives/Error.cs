using System.Collections.ObjectModel;

namespace ResultCrafter.Core.Primitives;

/// <summary>
///    An immutable, typed error value. Carry one through a <see cref="Result{T}" /> or
///    <see cref="Result" /> rather than throwing exceptions for expected failure paths.
/// </summary>
public readonly struct Error : IEquatable<Error>
{
   /// <summary>The category of this error. Determines the HTTP status code and ProblemDetails title.</summary>
   public ErrorType Type { get; }

   /// <summary>
   ///    A human-readable description of the error. Falls back to a catalog default when
   ///    <see langword="null" />. Always populated for <see cref="BadRequest(string)" />.
   /// </summary>
   public string? Detail { get; }

   /// <summary>
   ///    Field-level error messages, keyed by field name. Only populated for errors
   ///    constructed via <see cref="BadRequest(Dictionary{string,string[]},string?)" />.
   ///    <see langword="null" /> otherwise.
   /// </summary>
   public IReadOnlyDictionary<string, string[]>? Errors { get; }

   /// <summary>
   ///    <see langword="true" /> when this error carries a field-level <see cref="Errors" /> dictionary.
   /// </summary>
   public bool IsValidation => Errors is not null;

   private Error(ErrorType type, string? detail, IReadOnlyDictionary<string, string[]>? errors)
   {
      if (type == ErrorType.None)
      {
         throw new ArgumentException(
            "ErrorType.None is not a valid error type. Use one of the Error factory methods.",
            nameof(type));
      }

      Type = type;
      Detail = detail;
      Errors = errors;
   }

   // ── Simple errors ─────────────────────────────────────────────────────────

   /// <summary>Creates a <see cref="ErrorType.NotFound" /> error (HTTP 404).</summary>
   /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
   public static Error NotFound(string? detail = null)
   {
      return new Error(ErrorType.NotFound, detail, null);
   }

   /// <summary>Creates an <see cref="ErrorType.Unauthorized" /> error (HTTP 401).</summary>
   /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
   public static Error Unauthorized(string? detail = null)
   {
      return new Error(ErrorType.Unauthorized, detail, null);
   }

   /// <summary>Creates a <see cref="ErrorType.Forbidden" /> error (HTTP 403).</summary>
   /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
   public static Error Forbidden(string? detail = null)
   {
      return new Error(ErrorType.Forbidden, detail, null);
   }

   /// <summary>Creates a <see cref="ErrorType.Conflict" /> error (HTTP 409).</summary>
   /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
   public static Error Conflict(string? detail = null)
   {
      return new Error(ErrorType.Conflict, detail, null);
   }

   /// <summary>Creates a <see cref="ErrorType.ConcurrencyConflict" /> error (HTTP 409).</summary>
   /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
   public static Error ConcurrencyConflict(string? detail = null)
   {
      return new Error(ErrorType.ConcurrencyConflict, detail, null);
   }

   // ── BadRequest ────────────────────────────────────────────────────────────

   /// <summary>
   ///    Creates a <see cref="ErrorType.BadRequest" /> error (HTTP 400) with a prose reason.
   /// </summary>
   /// <param name="detail">A non-empty, human-readable description of the problem.</param>
   /// <exception cref="ArgumentException">Thrown when <paramref name="detail" /> is null, empty, or whitespace.</exception>
   public static Error BadRequest(string detail)
   {
      ArgumentException.ThrowIfNullOrWhiteSpace(detail);
      return new Error(ErrorType.BadRequest, detail, null);
   }

   /// <summary>
   ///    Creates a <see cref="ErrorType.BadRequest" /> error (HTTP 400) with field-level validation errors.
   ///    The passed dictionary and arrays are defensively copied.
   /// </summary>
   /// <param name="errors">
   ///    A non-empty dictionary mapping field names to one or more error messages.
   ///    Keys are normalised to case-insensitive comparison. At least one entry is required.
   /// </param>
   /// <param name="detail">Optional prose summary. Falls back to a catalog default when <see langword="null" />.</param>
   /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors" /> is <see langword="null" />.</exception>
   /// <exception cref="ArgumentException">
   ///    Thrown when <paramref name="errors" /> is empty or contains a null/whitespace key.
   /// </exception>
   public static Error BadRequest(Dictionary<string, string[]> errors, string? detail = null)
   {
      ArgumentNullException.ThrowIfNull(errors);

      if (errors.Count == 0)
      {
         throw new ArgumentException("BadRequest(errors) requires at least one field error.", nameof(errors));
      }

      // Copy + normalize keys to case-insensitive (matches how validators aggregate).
      var copy = new Dictionary<string, string[]>(errors.Count, StringComparer.OrdinalIgnoreCase);

      foreach (var (key, value) in errors)
      {
         ArgumentException.ThrowIfNullOrWhiteSpace(key);
         // Clone array so caller can't mutate after creating Error.
         copy[key] = value.Length == 0 ? [] : (string[])value.Clone();
      }

      return new Error(ErrorType.BadRequest, detail, new ReadOnlyDictionary<string, string[]>(copy));
   }

   // ── Equality ──────────────────────────────────────────────────────────────

   /// <inheritdoc />
   public bool Equals(Error other)
   {
      return Type == other.Type &&
             string.Equals(Detail, other.Detail, StringComparison.Ordinal) &&
             ErrorsEqual(Errors, other.Errors);
   }

   /// <inheritdoc />
   public override bool Equals(object? obj)
   {
      return obj is Error other && Equals(other);
   }

   /// <inheritdoc />
   public override int GetHashCode()
   {
      unchecked
      {
         var hash = 17;
         hash = hash * 31 + (int)Type;
         hash = hash * 31 + (Detail is null ? 0 : StringComparer.Ordinal.GetHashCode(Detail));

         if (Errors is null)
         {
            return hash;
         }

         hash = hash * 31 + Errors.Count;

         var entriesHash = 0;
         foreach (var (key, values) in Errors)
         {
            var entry = HashCode.Combine(
               StringComparer.OrdinalIgnoreCase.GetHashCode(key),
               values.Length);

            entry = values.Aggregate(entry,
               (current, v) => HashCode.Combine(current, StringComparer.Ordinal.GetHashCode(v)));

            // Addition instead of XOR — avoids self-cancellation of duplicate entries.
            entriesHash += entry;
         }

         hash = hash * 31 + entriesHash;

         return hash;
      }
   }

   /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are equal.</summary>
   public static bool operator ==(Error left, Error right)
   {
      return left.Equals(right);
   }

   /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are not equal.</summary>
   public static bool operator !=(Error left, Error right)
   {
      return !left.Equals(right);
   }

   private static bool ErrorsEqual(IReadOnlyDictionary<string, string[]>? a,
      IReadOnlyDictionary<string, string[]>? b)
   {
      if (ReferenceEquals(a, b))
      {
         return true;
      }

      if (a is null || b is null || a.Count != b.Count)
      {
         return false;
      }

      foreach (var (key, aValues) in a)
      {
         if (!b.TryGetValue(key, out var bValues))
         {
            return false;
         }

         if (!ArrayEquals(aValues, bValues))
         {
            return false;
         }
      }

      return true;
   }

   private static bool ArrayEquals(string[]? a, string[]? b)
   {
      if (ReferenceEquals(a, b))
      {
         return true;
      }

      if (a is null || b is null || a.Length != b.Length)
      {
         return false;
      }

      for (var i = 0; i < a.Length; i++)
      {
         if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
         {
            return false;
         }
      }

      return true;
   }

   /// <inheritdoc />
   public override string ToString()
   {
      return Detail is not null ? $"{Type}: {Detail}" : Type.ToString();
   }
}