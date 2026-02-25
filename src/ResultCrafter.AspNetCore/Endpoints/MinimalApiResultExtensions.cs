using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ResultCrafter.AspNetCore.ProblemDetails;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.AspNetCore.Endpoints;

/// <summary>
/// Extension methods that map <see cref="Result{T}"/>, <see cref="Result"/>, and
/// <see cref="Error"/> values to typed <c>IResult</c> responses for use in Minimal API
/// endpoint handlers.
/// </summary>
/// <remarks>
/// On the success path these methods delegate to the <c>TypedResults</c> factory so
/// that ASP.NET Core's OpenAPI tooling can infer the response shape. On the failure path
/// they produce an RFC 9457 <c>ProblemDetails</c> response via
/// <see cref="ProblemDetailsBuilder"/>.
/// </remarks>
public static class MinimalApiResultExtensions
{
   extension<T>(Result<T> result)
   {
      /// <summary>
      /// Maps a <see cref="Result{T}"/> to <c>200 Ok</c> on success or
      /// <c>ProblemDetails</c> on failure.
      /// </summary>
      public Results<Ok<T>, ProblemHttpResult>
         ToOkResult() =>
         result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : CreateProblem(result.Error!.Value);

      /// <summary>
      /// Maps a <see cref="Result{T}"/> to <c>201 Created</c> on success or
      /// <c>ProblemDetails</c> on failure.
      /// </summary>
      /// <remarks>
      /// The result must have been constructed via <see cref="Result{T}.Created"/>;
      /// calling this on an <c>Ok</c> or <c>Accepted</c> result is a programming error
      /// and will throw <see cref="InvalidOperationException"/>.
      /// </remarks>
      public Results<Created<T>, ProblemHttpResult>
         ToCreatedResult()
      {
         if (!result.IsSuccess)
            return CreateProblem(result.Error!.Value);

         if (result.Kind != SuccessKind.Created || result.Location is null)
            throw new InvalidOperationException(
               $"ToCreatedResult requires a Result<T> constructed via Result<T>.Created(string, T). " +
               $"Got Kind={result.Kind}, Location={(result.Location is null ? "null" : $"\"{result.Location}\"")}.");

         return TypedResults.Created(result.Location, result.Value!);
      }

      /// <summary>
      /// Maps a <see cref="Result{T}"/> to <c>202 Accepted</c> on success or
      /// <c>ProblemDetails</c> on failure.
      /// </summary>
      public Results<Accepted<T>, ProblemHttpResult>
         ToAcceptedResult() =>
         result.IsSuccess
            ? TypedResults.Accepted(result.Location, result.Value!)
            : CreateProblem(result.Error!.Value);
   }

   extension(Result result)
   {
      /// <summary>
      /// Maps a void <see cref="Result"/> to <c>204 NoContent</c> on success or
      /// <c>ProblemDetails</c> on failure.
      /// </summary>
      public Results<NoContent, ProblemHttpResult>
         ToNoContentResult() =>
         result.IsSuccess
            ? TypedResults.NoContent()
            : CreateProblem(result.Error!.Value);

      /// <summary>
      /// Maps a void <see cref="Result"/> to <c>202 Accepted</c> on success or
      /// <c>ProblemDetails</c> on failure.
      /// </summary>
      public Results<Accepted, ProblemHttpResult>
         ToAcceptedResult() =>
         result.IsSuccess
            ? TypedResults.Accepted(result.AcceptedLocation)
            : CreateProblem(result.Error!.Value);
   }


   /// <summary>Maps a bare <see cref="Error"/> directly to a <c>ProblemDetails</c> response.</summary>
   public static ProblemHttpResult ToProblemResult(this Error error) => CreateProblem(error);

   // ── Private helpers ───────────────────────────────────────────────────────

   private static ProblemHttpResult CreateProblem(Error error)
   {
      var pd = ProblemDetailsBuilder.Build(error);
      return TypedResults.Problem(
         detail: pd.Detail,
         statusCode: pd.Status,
         title: pd.Title,
         extensions: pd.Extensions);
   }
}