namespace ResultCrafter.Core.Primitives;

/// <summary>
///    Categorises the failure mode of an <see cref="Error" />, determining the HTTP
///    status code and ProblemDetails title that ResultCrafter emits.
/// </summary>
public enum ErrorType
{
   /// <summary>
   ///    The CLR default value. Not a valid error type. Exists so that
   ///    <c>default(Error)</c> and <c>default(ErrorType)</c> are explicitly
   ///    invalid rather than silently mapping to <see cref="BadRequest" />.
   /// </summary>
   None = 0,

   /// <summary>
   ///    A business-rule violation or invalid input (HTTP 400).
   ///    When constructed with a field-errors dictionary via
   ///    <see cref="Error.BadRequest(Dictionary{string, string[]}, string?)" />,
   ///    the response uses the structured RFC 9457 <c>ValidationProblem</c> shape
   ///    with the <c>errors</c> dictionary at the top level of the body.
   ///    Without a dictionary, a plain <c>ProblemDetails</c> response is returned.
   /// </summary>
   BadRequest = 1,

   /// <summary>The resource or entity could not be found. Maps to HTTP 404.</summary>
   NotFound = 2,

   /// <summary>A write conflict was detected. Maps to HTTP 409.</summary>
   Conflict = 3,

   /// <summary>The caller is not authenticated. Maps to HTTP 401.</summary>
   Unauthorized = 4,

   /// <summary>The caller is authenticated but lacks the required permission. Maps to HTTP 403.</summary>
   Forbidden = 5,

   /// <summary>
   ///    Optimistic-concurrency token mismatch (HTTP 409).
   ///    The caller should re-fetch and retry.
   /// </summary>
   ConcurrencyConflict = 6
}