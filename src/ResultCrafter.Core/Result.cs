using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ResultCrafter;

/// <summary>
///     The outcome of a void operation, one that produces no value on success: either success or an
///     <see cref="Error" />.
/// </summary>
/// <remarks>
///     Use <see cref="Result{T}" /> when the success path returns a value.
///     Choose the factory that matches the intended HTTP status code:
///     <list type="bullet">
///         <item><see cref="NoContent" /> → 204 No Content</item>
///         <item><see cref="Accepted" /> → 202 Accepted (optionally with a poll URL)</item>
///         <item><see cref="Fail" /> / implicit conversion from <see cref="Error" /> → error response</item>
///     </list>
///     <para>
///         <c>default(Result)</c> skips every factory, so it is neither a success nor a failure.
///         <see cref="IsSuccess" />, <see cref="Match{TOut}" /> and the ASP.NET Core <c>To*Result</c> methods
///         throw <see cref="InvalidOperationException" /> on it.
///     </para>
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Result : IEquatable<Result>
{
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
    ///     Optional poll URL for <see cref="Accepted" /> results, sent as the HTTP <c>Location</c> header.
    ///     <see langword="null" /> for all other outcomes.
    /// </summary>
    public string? Location { get; }

    /// <summary>
    ///     <see langword="true" /> when the operation succeeded;
    ///     <see langword="false" /> when it produced an <see cref="Error" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown on a <c>default(Result)</c> instance.</exception>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess
    {
        get
        {
            if (Error is not null) return false;
            if (Kind == SuccessKind.None) throw DefaultInstance();

            return true;
        }
    }

    private Result(Error? error, SuccessKind kind, string? location)
    {
        Error = error;
        Kind = kind;
        Location = location;
    }

    /// <summary>Creates a 204 No Content success result.</summary>
    public static Result NoContent()
    {
        return new Result(null, SuccessKind.NoContent, null);
    }

    /// <summary>
    ///     Creates a 202 Accepted success result. Provide <paramref name="location" />
    ///     when the caller should poll a URL to confirm completion.
    /// </summary>
    public static Result Accepted(string? location = null)
    {
        return new Result(null, SuccessKind.Accepted, location);
    }

    /// <summary>Creates a failure result carrying the given <paramref name="error" />.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <see langword="null" />.</exception>
    public static Result Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(error, SuccessKind.None, null);
    }

    /// <summary>
    ///     Implicitly wraps <paramref name="error" /> as a failure result.
    ///     Enables <c>return Error.NotFound();</c> in methods returning <see cref="Result" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <see langword="null" />.</exception>
    public static implicit operator Result(Error error)
    {
        return Fail(error);
    }

    /// <summary>
    ///     Runs <paramref name="onSuccess" /> or <paramref name="onFailure" /> with the error,
    ///     collapsing both paths into a single <typeparamref name="TOut" />.
    /// </summary>
    /// <typeparam name="TOut">The type both branches produce.</typeparam>
    /// <param name="onSuccess">Invoked when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with <see cref="Error" /> when the operation failed.</param>
    /// <exception cref="InvalidOperationException">Thrown on a <c>default</c> instance.</exception>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    private static InvalidOperationException DefaultInstance()
    {
        return new InvalidOperationException(
            "This is a default(Result), which is invalid: no factory created it, so it is neither a success " +
            "nor a failure. Create results with Result.NoContent, Result.Accepted or Result.Fail, or the " +
            "implicit conversion from an Error.");
    }

    // Reads fields directly: the debugger must be able to show a default instance without throwing.
    private string DebuggerDisplay => Error is not null
        ? $"Failure({Error})"
        : Kind == SuccessKind.None
            ? "Invalid(default)"
            : $"Success({Kind})";

    /// <inheritdoc />
    public bool Equals(Result other)
    {
        return Error == other.Error && Kind == other.Kind && Location == other.Location;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Result other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Error, Kind, Location);
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are equal.</summary>
    public static bool operator ==(Result left, Result right)
    {
        return left.Equals(right);
    }

    /// <summary>Returns <see langword="true" /> when <paramref name="left" /> and <paramref name="right" /> are not equal.</summary>
    public static bool operator !=(Result left, Result right)
    {
        return !left.Equals(right);
    }
}
