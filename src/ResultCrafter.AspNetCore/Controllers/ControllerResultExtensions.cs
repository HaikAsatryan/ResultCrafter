using Microsoft.AspNetCore.Mvc;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.AspNetCore.Controllers;

/// <summary>
///    Extension methods that map <see cref="Result{T}" />, <see cref="Result" />, and
///    <see cref="Error" /> values to typed <c>ActionResult</c> responses for use in MVC
///    controller actions.
/// </summary>
/// <remarks>
///    On the success path these methods return the appropriate typed <c>ActionResult</c>
///    (e.g. <see cref="OkObjectResult" />, <see cref="CreatedResult" />). On the failure
///    path they return a <see cref="ProblemActionResult" /> that routes through
///    <see cref="Microsoft.AspNetCore.Http.IProblemDetailsService" />, giving every controller
///    error response the same RFC 9457 shape, enrichment, and structured logging as a
///    Minimal API error response.
///    <para>
///       Decorate controller actions with the ResultCrafter attribute family
///       (<c>[ProducesNotFound]</c>, <c>[ProducesBadRequest]</c>, etc.) to add the
///       corresponding ProblemDetails status codes to the OpenAPI schema automatically.
///    </para>
/// </remarks>
public static class ControllerResultExtensions
{
   /// <summary>Maps a bare <see cref="Error" /> directly to a <c>ProblemDetails</c> response.</summary>
   public static IActionResult ToProblemResult(this Error error)
   {
      return new ProblemActionResult(error);
   }

   /// <summary>
   ///    Maps a <see cref="Result{T}" /> to <c>200 Ok</c> on success or
   ///    <c>ProblemDetails</c> on failure.
   /// </summary>
   public static ActionResult<T> ToOkResult<T>(this Result<T> result)
   {
      return result.IsSuccess
         ? new OkObjectResult(result.Value!)
         : new ProblemActionResult(result.Error!.Value);
   }

   /// <summary>
   ///    Maps a <see cref="Result{T}" /> to <c>201 Created</c> on success or
   ///    <c>ProblemDetails</c> on failure.
   /// </summary>
   /// <remarks>
   ///    The result must have been constructed via <see cref="Result{T}.Created" />;
   ///    calling this on an <c>Ok</c> or <c>Accepted</c> result is a programming error
   ///    and will throw <see cref="InvalidOperationException" />.
   /// </remarks>
   public static ActionResult<T> ToCreatedResult<T>(this Result<T> result)
   {
      if (!result.IsSuccess)
      {
         return new ProblemActionResult(result.Error!.Value);
      }

      if (result.Kind != SuccessKind.Created || result.Location is null)
      {
         throw new InvalidOperationException(
            $"ToCreatedResult requires a Result<T> constructed via Result<T>.Created(string, T). " +
            $"Got Kind={result.Kind}, Location={(result.Location is null ? "null" : $"{result.Location}")}.");
      }

      return new CreatedResult(result.Location, result.Value!);
   }

   /// <summary>
   ///    Maps a <see cref="Result{T}" /> to <c>202 Accepted</c> on success or
   ///    <c>ProblemDetails</c> on failure.
   /// </summary>
   public static ActionResult<T> ToAcceptedResult<T>(this Result<T> result)
   {
      return result.IsSuccess
         ? new AcceptedResult(result.Location, result.Value!)
         : new ProblemActionResult(result.Error!.Value);
   }

   /// <summary>
   ///    Maps a void <see cref="Result" /> to <c>204 NoContent</c> on success or
   ///    <c>ProblemDetails</c> on failure.
   /// </summary>
   /// <remarks>
   ///    The result must have been constructed via <see cref="Result.NoContent()" />;
   ///    calling this on an <c>Accepted</c> result is a programming error
   ///    and will throw <see cref="InvalidOperationException" />.
   /// </remarks>
   public static IActionResult ToNoContentResult(this Result result)
   {
      if (!result.IsSuccess)
      {
         return new ProblemActionResult(result.Error!.Value);
      }

      if (result.Kind != SuccessKind.NoContent)
      {
         throw new InvalidOperationException(
            $"ToNoContentResult requires a Result constructed via Result.NoContent(). Got Kind={result.Kind}.");
      }

      return new NoContentResult();
   }

   /// <summary>
   ///    Maps a void <see cref="Result" /> to <c>202 Accepted</c> on success or
   ///    <c>ProblemDetails</c> on failure.
   /// </summary>
   /// <remarks>
   ///    The result must have been constructed via <see cref="Result.Accepted" />;
   ///    calling this on a <c>NoContent</c> result is a programming error
   ///    and will throw <see cref="InvalidOperationException" />.
   /// </remarks>
   public static IActionResult ToAcceptedResult(this Result result)
   {
      if (!result.IsSuccess)
      {
         return new ProblemActionResult(result.Error!.Value);
      }

      if (result.Kind != SuccessKind.Accepted)
      {
         throw new InvalidOperationException(
            $"ToAcceptedResult requires a Result constructed via Result.Accepted(). Got Kind={result.Kind}.");
      }

      return new AcceptedResult(result.AcceptedLocation, null);
   }
}