using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ResultCrafter;

/// <summary>
///     The outcome of an operation that returns a value: either a success value or an <see cref="Error" />.
///     Use <see cref="Ok" />, <see cref="Created" />, or <see cref="Accepted" /> for success and
///     <see cref="Fail" /> (or an implicit conversion from <see cref="Error" />) for failure.
/// </summary>
/// <remarks>
///     <c>default(Result&lt;T&gt;)</c> skips every factory, so it is neither a success nor a failure.
///     <see cref="IsSuccess" />, <see cref="Map{TOut}" />, <see cref="Match{TOut}" /> and the ASP.NET Core
///     <c>To*Result</c> methods throw <see cref="InvalidOperationException" /> on it.
/// </remarks>
/// <typeparam name="T">The value type returned on the success path.</typeparam>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Result<T> : IEquatable<Result<T>>
{
    /// <summary>
    ///     The success value. Non-null whenever <see cref="IsSuccess" /> is <see langword="true" />
    ///     (all success factories reject null); <see langword="default" /> on failure.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    ///     The error produced by this operation. Non-null whenever <see cref="IsSuccess" /> is
    ///     <see langword="false" />; <see langword="null" /> on success.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    ///     The HTTP success variant this result maps to. <see cref="SuccessKind.None" /> on failure.
    /// </summary>
    public SuccessKind Kind { get; }

    /// <summary>
    ///     Resource URI used for HTTP Location headers.
    ///     Required for <see cref="SuccessKind.Created" />; optional poll URL for
    ///     <see cref="SuccessKind.Accepted" />. <see langword="null" /> for all other outcomes.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    ///     <see langword="true" /> when the operation succeeded;
    ///     <see langword="false" /> when it produced an <see cref="Error" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown on a <c>default(Result&lt;T&gt;)</c> instance.</exception>
    [MemberNotNullWhen(false, nameof(Error))]
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess
    {
        get
        {
            if (Error is not null) return false;
            if (Kind == SuccessKind.None) throw DefaultInstance();

            // Every success factory rejects null, so a success always carries a value.
            Debug.Assert(Value is not null);
            return true;
        }
    }

    private Result(T? value, Error? error, SuccessKind kind, string? location)
    {
        Value = value;
        Error = error;
        Kind = kind;
        Location = location;
    }

    /// <summary>Creates a 200 OK success result.</summary>
    /// <param name="value">The success value. Must not be null; use the void <see cref="Result" /> for value-less success.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public static Result<T> Ok(T value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        return new Result<T>(value, null, SuccessKind.Ok, null);
    }

    /// <summary>
    ///     Creates a 201 Created success result.
    /// </summary>
    /// <param name="location">
    ///     The URI of the newly created resource. Sent as the HTTP <c>Location</c> header.
    /// </param>
    /// <param name="value">The created resource representation. Must not be null.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="location" /> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public static Result<T> Created(string location, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        return value is null
            ? throw new ArgumentNullException(nameof(value))
            : new Result<T>(value, null, SuccessKind.Created, location);
    }

    /// <summary>
    ///     Creates a 202 Accepted success result. Use when the request has been queued
    ///     or handed off to a background process and completion is not yet confirmed.
    /// </summary>
    /// <param name="value">A representation of the resource as it stands at acceptance time. Must not be null.</param>
    /// <param name="location">
    ///     Optional URL the caller can poll to check completion status.
    ///     Sent as the HTTP <c>Location</c> header when provided.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public static Result<T> Accepted(T value, string? location = null)
    {
        return value is null
            ? throw new ArgumentNullException(nameof(value))
            : new Result<T>(value, null, SuccessKind.Accepted, location);
    }

    /// <summary>Creates a failure result carrying the given <paramref name="error" />.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <see langword="null" />.</exception>
    public static Result<T> Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(default, error, SuccessKind.None, null);
    }

    // Lets Map keep the original success variant and location.
    private static Result<T> Success(T value, SuccessKind kind, string? location)
    {
        return new Result<T>(value, null, kind, location);
    }

    /// <summary>
    ///     Implicitly wraps <paramref name="value" /> as an <see cref="Ok" /> result.
    ///     Enables <c>return item;</c> in methods returning <c>Result&lt;T&gt;</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <see langword="null" />.</exception>
    public static implicit operator Result<T>(T value)
    {
        return Ok(value);
    }

    /// <summary>
    ///     Implicitly wraps <paramref name="error" /> as a failure result.
    ///     Enables <c>return Error.NotFound();</c> in methods returning <c>Result&lt;T&gt;</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <see langword="null" />.</exception>
    public static implicit operator Result<T>(Error error)
    {
        return Fail(error);
    }

    /// <summary>
    ///     Transforms the success value using <paramref name="selector" />, preserving
    ///     <see cref="Kind" /> and <see cref="Location" />. On the failure path, the
    ///     error is propagated unchanged without invoking <paramref name="selector" />.
    /// </summary>
    /// <typeparam name="TOut">The type returned by the transform function.</typeparam>
    /// <param name="selector">A function that maps <typeparamref name="T" /> to <typeparamref name="TOut" />.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="selector" /> returns <see langword="null" />, or on a <c>default</c> instance.
    /// </exception>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!IsSuccess) return Result<TOut>.Fail(Error);

        var mapped = selector(Value) ?? throw new InvalidOperationException(
            "Map selector returned null; a success Result<T> cannot carry a null value.");

        return Result<TOut>.Success(mapped, Kind, Location);
    }

    /// <summary>
    ///     Runs <paramref name="onSuccess" /> with the success value or
    ///     <paramref name="onFailure" /> with the error, collapsing both paths into a
    ///     single <typeparamref name="TOut" />.
    /// </summary>
    /// <typeparam name="TOut">The type both branches produce.</typeparam>
    /// <param name="onSuccess">Invoked with <see cref="Value" /> when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with <see cref="Error" /> when the operation failed.</param>
    /// <exception cref="InvalidOperationException">Thrown on a <c>default</c> instance.</exception>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }

    private static InvalidOperationException DefaultInstance()
    {
        return new InvalidOperationException(
            "This is a default(Result<T>), which is invalid: no factory created it, so it is neither a success " +
            "nor a failure. Create results with Result<T>.Ok, Created, Accepted or Fail, or the implicit " +
            "conversions from a value or an Error.");
    }

    // Reads fields directly: the debugger must be able to show a default instance without throwing.
    private string DebuggerDisplay => Error is not null
        ? $"Failure({Error})"
        : Kind == SuccessKind.None
            ? "Invalid(default)"
            : $"Success({Kind}): {Value}";

    /// <inheritdoc />
    public bool Equals(Result<T> other)
    {
        return EqualityComparer<T?>.Default.Equals(Value, other.Value) &&
               Error == other.Error &&
               Kind == other.Kind &&
               Location == other.Location;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Result<T> other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Error, Kind, Location);
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are equal.</summary>
    public static bool operator ==(Result<T> left, Result<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are not equal.</summary>
    public static bool operator !=(Result<T> left, Result<T> right)
    {
        return !left.Equals(right);
    }
}
