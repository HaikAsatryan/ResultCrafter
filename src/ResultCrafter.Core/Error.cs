using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace ResultCrafter;

/// <summary>
///     A typed error whose members cannot be reassigned. Carry one through a <see cref="Result{T}" /> or
///     <see cref="Result" /> instead of throwing exceptions for expected failures. Create it with a factory such as
///     <see cref="NotFound" />; there is no public constructor.
/// </summary>
/// <remarks>
///     <see cref="Error" /> is a sealed class, not a struct. A result then holds one reference instead of a
///     copy of every field, which roughly halves its size, and there is no <c>default(Error)</c> that skips the
///     factory checks. Equality compares values, not references. The arrays in <see cref="Errors" /> and
///     string-array <see cref="Extensions" /> values are the error's own: treat them as read-only. Changing them
///     changes the error and its hash code. The ASP.NET Core integration copies them into each response.
/// </remarks>
public sealed class Error : IEquatable<Error>
{
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(int.MaxValue);

    private static readonly IReadOnlyDictionary<string, object> NoExtensions = TypedValues.Empty;

    // Members that a ProblemDetails body or the ResultCrafter pipeline already writes. An extension with
    // one of these names would overwrite them or collide with them.
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "type", "title", "status", "detail", "instance", "errors", "traceId", "requestId"
    };

    // Messages and field errors sit behind one reference, so field errors cost no extra field on every Error.
    private readonly ValidationErrors? _validation;

    private Error(ErrorType type,
        string? detail,
        ValidationErrors? validation,
        TimeSpan? retryAfter,
        IReadOnlyDictionary<string, object> extensions)
    {
        Type = type;
        Detail = detail;
        _validation = validation;
        RetryAfter = retryAfter;
        Extensions = extensions;
    }

    /// <summary>The category of this error. Determines the HTTP status code and ProblemDetails title.</summary>
    public ErrorType Type { get; }

    /// <summary>
    ///     A human-readable description of the error. Falls back to a catalog default when
    ///     <see langword="null" />. Always set for <see cref="BadRequest(string)" />.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    ///     Field-level error messages, keyed by field name. Set only by
    ///     <see cref="BadRequest(IReadOnlyDictionary{string,string[]},string?)" /> and
    ///     <see cref="BadRequest(IReadOnlyDictionary{string,FieldError[]},string?)" />; <see langword="null" />
    ///     otherwise. The message arrays are the error's own; do not modify them.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors => _validation?.Messages;

    /// <summary>
    ///     The field errors with their codes and args, keyed like <see cref="Errors" />: entry <c>i</c> of a field holds
    ///     message <c>i</c> of the same field. Set only by
    ///     <see cref="BadRequest(IReadOnlyDictionary{string,FieldError[]},string?)" />; <see langword="null" />
    ///     otherwise. The arrays are the error's own; do not modify them.
    /// </summary>
    public IReadOnlyDictionary<string, FieldError[]>? FieldErrors => _validation?.Entries;

    /// <summary>
    ///     <see langword="true" /> when this error carries a field-level <see cref="Errors" /> dictionary.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Errors))]
    public bool IsValidation => Errors is not null;

    /// <summary>
    ///     How long the caller should wait before retrying. Set only by <see cref="TooManyRequests" />;
    ///     sent as the HTTP <c>Retry-After</c> header in whole seconds, rounded up.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    ///     Extra members added with the <c>With</c> methods. They are written as top-level members of the
    ///     ProblemDetails body, with the key exactly as given. Empty when none were added.
    /// </summary>
    public IReadOnlyDictionary<string, object> Extensions { get; }

    // ── Simple errors ─────────────────────────────────────────────────────────

    /// <summary>Creates a <see cref="ErrorType.NotFound" /> error (HTTP 404).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error NotFound(string? detail = null)
    {
        return Create(ErrorType.NotFound, detail);
    }

    /// <summary>Creates an <see cref="ErrorType.Unauthorized" /> error (HTTP 401).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error Unauthorized(string? detail = null)
    {
        return Create(ErrorType.Unauthorized, detail);
    }

    /// <summary>Creates a <see cref="ErrorType.Forbidden" /> error (HTTP 403).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error Forbidden(string? detail = null)
    {
        return Create(ErrorType.Forbidden, detail);
    }

    /// <summary>Creates a <see cref="ErrorType.Conflict" /> error (HTTP 409).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error Conflict(string? detail = null)
    {
        return Create(ErrorType.Conflict, detail);
    }

    /// <summary>Creates a <see cref="ErrorType.ConcurrencyConflict" /> error (HTTP 409).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error ConcurrencyConflict(string? detail = null)
    {
        return Create(ErrorType.ConcurrencyConflict, detail);
    }

    /// <summary>Creates a <see cref="ErrorType.Gone" /> error (HTTP 410).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error Gone(string? detail = null)
    {
        return Create(ErrorType.Gone, detail);
    }

    /// <summary>Creates an <see cref="ErrorType.UnprocessableEntity" /> error (HTTP 422).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    public static Error UnprocessableEntity(string? detail = null)
    {
        return Create(ErrorType.UnprocessableEntity, detail);
    }

    /// <summary>Creates a <see cref="ErrorType.TooManyRequests" /> error (HTTP 429).</summary>
    /// <param name="detail">Optional human-readable description. Falls back to a catalog default when <see langword="null" />.</param>
    /// <param name="retryAfter">
    ///     Optional wait before the caller should retry. Sent as the <c>Retry-After</c> header in whole
    ///     seconds, rounded up. Must be positive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="retryAfter" /> is zero, negative, or longer than <see cref="int.MaxValue" />
    ///     seconds (clients such as .NET's <c>HttpClient</c> cannot read a larger value).
    /// </exception>
    public static Error TooManyRequests(string? detail = null, TimeSpan? retryAfter = null)
    {
        if (retryAfter is { } wait && (wait <= TimeSpan.Zero || wait > MaxRetryAfter))
            throw new ArgumentOutOfRangeException(nameof(retryAfter), wait,
                "Retry-After must be positive and at most int.MaxValue seconds.");

        return new Error(ErrorType.TooManyRequests, detail, null, retryAfter, NoExtensions);
    }

    // ── BadRequest ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a <see cref="ErrorType.BadRequest" /> error (HTTP 400) with a prose reason.
    /// </summary>
    /// <param name="detail">A non-empty, human-readable description of the problem.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="detail" /> is null, empty, or whitespace.</exception>
    public static Error BadRequest(string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return Create(ErrorType.BadRequest, detail);
    }

    /// <summary>
    ///     Creates a <see cref="ErrorType.BadRequest" /> error (HTTP 400) with field-level validation errors.
    ///     The dictionary and arrays are copied, so later changes by the caller do not affect the error.
    /// </summary>
    /// <param name="errors">
    ///     A non-empty dictionary mapping field names to their error messages. Keys are compared
    ///     case-insensitively; keys that differ only by case are merged into one entry.
    /// </param>
    /// <param name="detail">Optional prose summary. Falls back to a catalog default when <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="errors" /> is empty, contains a null or whitespace key, or maps a key to a
    ///     null message array or an array with a null message.
    /// </exception>
    public static Error BadRequest(IReadOnlyDictionary<string, string[]> errors, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
            throw new ArgumentException("BadRequest(errors) requires at least one field error.", nameof(errors));

        // Case-insensitive keys match how validators aggregate field names.
        var copy = new Dictionary<string, string[]>(errors.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in errors)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (value is null)
                throw new ArgumentException($"Field '{key}' maps to a null message array.", nameof(errors));
            // A null message would go out as JSON null, which the documented errors schema does not allow.
            if (Array.Exists(value, message => message is null))
                throw new ArgumentException($"Field '{key}' holds a null message.", nameof(errors));

            // Source keys that differ only by case land on one entry; merge their messages instead of
            // silently dropping the earlier ones.
            var cloned = value.Length == 0 ? [] : (string[])value.Clone();
            copy[key] = copy.TryGetValue(key, out var existing) ? [.. existing, .. cloned] : cloned;
        }

        return new Error(ErrorType.BadRequest, detail,
            new ValidationErrors(new ReadOnlyDictionary<string, string[]>(copy), null), null, NoExtensions);
    }

    /// <summary>
    ///     Creates a <see cref="ErrorType.BadRequest" /> error (HTTP 400) with field errors that can carry a code
    ///     and args, so a client can show each one in its own language. <see cref="Errors" /> holds their messages.
    ///     The dictionary and arrays are copied, so later changes by the caller do not affect the error.
    /// </summary>
    /// <param name="errors">
    ///     A non-empty dictionary mapping field names to their errors. Keys are compared case-insensitively; keys
    ///     that differ only by case are merged into one entry.
    /// </param>
    /// <param name="detail">Optional prose summary. Falls back to a catalog default when <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="errors" /> is empty, contains a null or whitespace key, or maps a key to a
    ///     null array or an array with a null entry.
    /// </exception>
    public static Error BadRequest(IReadOnlyDictionary<string, FieldError[]> errors, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
            throw new ArgumentException("BadRequest(errors) requires at least one field error.", nameof(errors));

        var entries = new Dictionary<string, FieldError[]>(errors.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in errors)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (value is null)
                throw new ArgumentException($"Field '{key}' maps to a null array.", nameof(errors));
            if (Array.Exists(value, entry => entry is null))
                throw new ArgumentException($"Field '{key}' holds a null field error.", nameof(errors));

            var cloned = value.Length == 0 ? [] : (FieldError[])value.Clone();
            entries[key] = entries.TryGetValue(key, out var existing) ? [.. existing, .. cloned] : cloned;
        }

        var messages = new Dictionary<string, string[]>(entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries) messages[key] = Array.ConvertAll(value, entry => entry.Message);

        return new Error(ErrorType.BadRequest, detail,
            new ValidationErrors(new ReadOnlyDictionary<string, string[]>(messages),
                new ReadOnlyDictionary<string, FieldError[]>(entries)), null, NoExtensions);
    }

    private static Error Create(ErrorType type, string? detail)
    {
        return new Error(type, detail, null, null, NoExtensions);
    }

    // ── Extensions ────────────────────────────────────────────────────────────
    // Typed overloads instead of one object overload: these are the value types that the ASP.NET Core
    // integration can always write as JSON, including under NativeAOT where reflection-based JSON is off.

    /// <summary>Returns a copy of this error with a string extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value. Must not be <see langword="null" />.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public Error With(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with an <see cref="int" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, int value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="long" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, long value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="bool" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, bool value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="double" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value. Must be finite, because JSON has no NaN or infinity.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is NaN or infinite.</exception>
    public Error With(string key, double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "JSON has no representation for NaN or infinity.");

        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="decimal" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, decimal value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="Guid" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, Guid value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="DateTimeOffset" /> extension member.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value, written in ISO 8601 format.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, DateTimeOffset value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a <see cref="DateTime" /> extension member.</summary>
    /// <remarks>
    ///     Written in ISO 8601 as given: <c>Z</c> for <see cref="DateTimeKind.Utc" />, the local offset for
    ///     <see cref="DateTimeKind.Local" />, no offset for <see cref="DateTimeKind.Unspecified" />. Without this
    ///     overload a <see cref="DateTime" /> would convert to <see cref="DateTimeOffset" /> and take the server's
    ///     local offset.
    /// </remarks>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    public Error With(string key, DateTime value)
    {
        return WithExtension(key, value);
    }

    /// <summary>Returns a copy of this error with a string array extension member. The array is copied.</summary>
    /// <param name="key">The JSON member name, written as given. Must not be a reserved ProblemDetails name.</param>
    /// <param name="value">The values. Must not be <see langword="null" />.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is empty or reserved.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public Error With(string key, string[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithExtension(key, value.Length == 0 ? [] : (string[])value.Clone());
    }

    private Error WithExtension(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (IsReserved(key))
            throw new ArgumentException(
                $"'{key}' is reserved: ProblemDetails or ResultCrafter already writes a member with this name.",
                nameof(key));

        return new Error(Type, Detail, _validation, RetryAfter, TypedValues.With(Extensions, key, value));
    }

    // The x-rc prefix holds ResultCrafter's internal pipeline markers, current and future.
    private static bool IsReserved(string key)
    {
        return ReservedKeys.Contains(key) ||
               key.Equals("x-rc", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("x-rc-", StringComparison.OrdinalIgnoreCase);
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(Error? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Type == other.Type &&
               string.Equals(Detail, other.Detail, StringComparison.Ordinal) &&
               RetryAfter == other.RetryAfter &&
               ErrorsEqual(Errors, other.Errors) &&
               FieldErrorsEqual(FieldErrors, other.FieldErrors) &&
               TypedValues.Equal(Extensions, other.Extensions);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Error other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Detail, StringComparer.Ordinal);
        hash.Add(RetryAfter);

        // Codes and args are left out: errors that differ only there share a hash, and Equals tells them apart.
        if (Errors is not null)
        {
            // Summed so the result does not depend on dictionary order.
            var entries = 0;
            foreach (var (key, values) in Errors)
            {
                entries += HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key),
                    TypedValues.ArrayHash(values));
            }

            hash.Add(Errors.Count);
            hash.Add(entries);
        }

        if (Extensions.Count > 0) hash.Add(TypedValues.Hash(Extensions));

        return hash.ToHashCode();
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are equal.</summary>
    public static bool operator ==(Error? left, Error? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are not equal.</summary>
    public static bool operator !=(Error? left, Error? right)
    {
        return !(left == right);
    }

    private static bool ErrorsEqual(IReadOnlyDictionary<string, string[]>? a,
        IReadOnlyDictionary<string, string[]>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.Count != b.Count) return false;

        foreach (var (key, aValues) in a)
        {
            if (!b.TryGetValue(key, out var bValues) || !TypedValues.ArrayEquals(aValues, bValues)) return false;
        }

        return true;
    }

    // An error built from message strings has no codes or args, so it equals one built from field errors that
    // carry none and have the same messages.
    private static bool FieldErrorsEqual(IReadOnlyDictionary<string, FieldError[]>? a,
        IReadOnlyDictionary<string, FieldError[]>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null) return !HasCodeOrArgs(b!);
        if (b is null) return !HasCodeOrArgs(a);
        if (a.Count != b.Count) return false;

        foreach (var (key, aEntries) in a)
        {
            if (!b.TryGetValue(key, out var bEntries) || !aEntries.AsSpan().SequenceEqual(bEntries)) return false;
        }

        return true;
    }

    private static bool HasCodeOrArgs(IReadOnlyDictionary<string, FieldError[]> fieldErrors)
    {
        foreach (var entries in fieldErrors.Values)
        {
            if (Array.Exists(entries, entry => entry.Code is not null || entry.Args.Count > 0)) return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Detail is not null ? $"{Type}: {Detail}" : Type.ToString();
    }

    private sealed class ValidationErrors(
        IReadOnlyDictionary<string, string[]> messages,
        IReadOnlyDictionary<string, FieldError[]>? entries)
    {
        public IReadOnlyDictionary<string, string[]> Messages { get; } = messages;

        public IReadOnlyDictionary<string, FieldError[]>? Entries { get; } = entries;
    }
}
