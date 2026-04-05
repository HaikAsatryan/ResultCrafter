namespace ResultCrafter.Core.Primitives;

/// <summary>
///    Discriminated union representing the outcome of a void operation — one that produces
///    no value on the success path.
/// </summary>
/// <remarks>
///    Use <see cref="Result{T}" /> when the success path returns a value.
///    Choose the factory that matches the intended HTTP status code:
///    <list type="bullet">
///       <item><see cref="NoContent" /> → 204 No Content</item>
///       <item><see cref="Accepted" /> → 202 Accepted (optionally with a poll URL)</item>
///       <item><see cref="Fail" /> / implicit cast from <see cref="Error" /> → error response</item>
///    </list>
/// </remarks>
public readonly struct Result : IEquatable<Result>
{
   /// <summary>
   ///    The error produced by this operation. Only meaningful when
   ///    <see cref="IsSuccess" /> is <see langword="false" />.
   /// </summary>
   public Error? Error { get; }

   /// <summary>
   ///    The HTTP success variant this result maps to.
   ///    <see cref="SuccessKind.Ok" /> on failure paths (CLR default).
   /// </summary>
   public SuccessKind Kind { get; }

   /// <summary>
   ///    Optional poll URL for <see cref="Accepted" /> results. <see langword="null" />
   ///    for all other outcomes.
   /// </summary>
   public string? AcceptedLocation { get; }

   /// <summary>
   ///    <see langword="true" /> when the operation succeeded;
   ///    <see langword="false" /> when it produced an <see cref="Error" />.
   /// </summary>
   public bool IsSuccess => Error is null;

   private Result(Error? error, SuccessKind kind, string? acceptedLocation)
   {
      Error = error;
      Kind = kind;
      AcceptedLocation = acceptedLocation;
   }

   /// <summary>Creates a 204 No Content success result.</summary>
   public static Result NoContent()
   {
      return new Result(null, SuccessKind.NoContent, null);
   }

   /// <summary>
   ///    Creates a 202 Accepted success result. Provide <paramref name="location" />
   ///    when the caller should poll a URL to confirm completion.
   /// </summary>
   public static Result Accepted(string? location = null)
   {
      return new Result(null, SuccessKind.Accepted, location);
   }

   /// <summary>Creates a failure result carrying the given <paramref name="error" />.</summary>
   public static Result Fail(Error error)
   {
      return new Result(error, SuccessKind.Ok, null);
   }

   /// <summary>
   ///    Implicitly wraps <paramref name="error" /> as a failure result.
   ///    Enables <c>return Error.NotFound();</c> in methods returning <see cref="Result" />.
   /// </summary>
   public static implicit operator Result(Error error)
   {
      return Fail(error);
   }

   /// <inheritdoc />
   public bool Equals(Result other)
   {
      return Error == other.Error && Kind == other.Kind && AcceptedLocation == other.AcceptedLocation;
   }

   /// <inheritdoc />
   public override bool Equals(object? obj)
   {
      return obj is Result other && Equals(other);
   }

   /// <inheritdoc />
   public override int GetHashCode()
   {
      return HashCode.Combine(Error, Kind, AcceptedLocation);
   }

   /// <inheritdoc />
   public static bool operator ==(Result left, Result right)
   {
      return left.Equals(right);
   }

   /// <inheritdoc />
   public static bool operator !=(Result left, Result right)
   {
      return !left.Equals(right);
   }
}