namespace ResultCrafter;

/// <summary>
///     One failed rule on a field: the English message, plus an optional stable code and the values the message
///     needs, so a client can show the error in its own language. Pass it to
///     <see cref="Error.BadRequest(IReadOnlyDictionary{string,FieldError[]},string?)" />.
/// </summary>
/// <remarks>
///     Members cannot be reassigned: each <c>With</c> call returns a copy. Equality compares values, not references.
///     The ASP.NET Core integration writes <see cref="Code" /> and <see cref="Args" /> to the response body, so never
///     put the value the caller sent in <see cref="Args" />: it can be a password or personal data.
/// </remarks>
public sealed class FieldError : IEquatable<FieldError>
{
    /// <summary>Creates a field error.</summary>
    /// <param name="message">The English message for developers, logs, and clients that do not know the code.</param>
    /// <param name="code">
    ///     A stable code for the failed rule, such as <c>email_in_use</c>, that clients match and translate. Never
    ///     rename or reuse one once clients depend on it. <see langword="null" /> when the rule has none.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="message" /> is null, empty or whitespace, or when <paramref name="code" /> is
    ///     empty or whitespace.
    /// </exception>
    public FieldError(string message, string? code = null)
        : this(message, code, TypedValues.Empty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (code is not null) ArgumentException.ThrowIfNullOrWhiteSpace(code);
    }

    private FieldError(string message, string? code, IReadOnlyDictionary<string, object> args)
    {
        Message = message;
        Code = code;
        Args = args;
    }

    /// <summary>The English message.</summary>
    public string Message { get; }

    /// <summary>The stable code for the failed rule, or <see langword="null" /> when the rule has none.</summary>
    public string? Code { get; }

    /// <summary>
    ///     The values the message needs, such as <c>maxLength</c>, added with the <c>With</c> methods. Keys are
    ///     written as given. Empty when none were added.
    /// </summary>
    public IReadOnlyDictionary<string, object> Args { get; }

    // ── Args ──────────────────────────────────────────────────────────────────
    // The same typed overloads as Error.With, for the same reason: these are the value types the ASP.NET Core
    // integration can always write as JSON, including under NativeAOT.

    /// <summary>Returns a copy of this field error with a string arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value. Must not be <see langword="null" />.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public FieldError With(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with an <see cref="int" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, int value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="long" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, long value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="bool" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, bool value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="double" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value. Must be finite, because JSON has no NaN or infinity.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is NaN or infinite.</exception>
    public FieldError With(string key, double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "JSON has no representation for NaN or infinity.");

        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="decimal" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, decimal value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="Guid" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, Guid value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="DateTimeOffset" /> arg.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value, written in ISO 8601 format.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, DateTimeOffset value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a <see cref="DateTime" /> arg.</summary>
    /// <remarks>
    ///     Written in ISO 8601 as given, like <see cref="Error.With(string, DateTime)" />: <c>Z</c> for
    ///     <see cref="DateTimeKind.Utc" />, the local offset for <see cref="DateTimeKind.Local" />, no offset for
    ///     <see cref="DateTimeKind.Unspecified" />.
    /// </remarks>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    public FieldError With(string key, DateTime value)
    {
        return WithArg(key, value);
    }

    /// <summary>Returns a copy of this field error with a string array arg. The array is copied.</summary>
    /// <param name="key">The JSON member name, written as given.</param>
    /// <param name="value">The values. Must not be <see langword="null" />.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public FieldError With(string key, string[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WithArg(key, value.Length == 0 ? [] : (string[])value.Clone());
    }

    private FieldError WithArg(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new FieldError(Message, Code, TypedValues.With(Args, key, value));
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(FieldError? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Message, other.Message, StringComparison.Ordinal) &&
               string.Equals(Code, other.Code, StringComparison.Ordinal) &&
               TypedValues.Equal(Args, other.Args);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is FieldError other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Message, StringComparer.Ordinal);
        hash.Add(Code, StringComparer.Ordinal);
        if (Args.Count > 0) hash.Add(TypedValues.Hash(Args));
        return hash.ToHashCode();
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are equal.</summary>
    public static bool operator ==(FieldError? left, FieldError? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are not equal.</summary>
    public static bool operator !=(FieldError? left, FieldError? right)
    {
        return !(left == right);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Code is not null ? $"{Code}: {Message}" : Message;
    }
}
