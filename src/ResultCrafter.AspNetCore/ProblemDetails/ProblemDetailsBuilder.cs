using ResultCrafter.Core.Primitives;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///    Constructs <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" /> instances from
///    <see cref="Error" /> values, centralising status-code mapping, title resolution,
///    detail fallback, and internal marker extension population.
/// </summary>
/// <remarks>
///    The <c>x-rc</c> and <c>x-rc-error-id</c> extension keys are stripped from the
///    serialised response by <c>ConfigureResultCrafterProblemDetails</c> before the
///    body is written to the client.
/// </remarks>
public static class ProblemDetailsBuilder
{
   /// <summary>
   ///    Builds a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" /> from <paramref name="error" />.
   ///    The returned object is ready to be passed to <c>IProblemDetailsService.TryWriteAsync</c>
   ///    or converted to a <c>ProblemHttpResult</c> via <c>TypedResults.Problem</c>.
   /// </summary>
   public static Microsoft.AspNetCore.Mvc.ProblemDetails Build(Error error)
   {
      var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
      {
         Type = HttpErrorCatalog.TypeUri(error.Type),
         Status = HttpErrorCatalog.Status(error.Type),
         Title = HttpErrorCatalog.Title(error.Type),
         Detail = HttpErrorCatalog.ResolveDetail(error),
         Extensions =
         {
            [ProblemDetailsKeys.RcMarker] = true,
            [ProblemDetailsKeys.RcErrorId] = error.Type.ToString()
         }
      };

      // Field-level errors are surfaced under the "errors" extension key,
      // matching the shape ASP.NET Core uses for model-validation responses.
      if (error.IsValidation)
      {
         pd.Extensions[ProblemDetailsKeys.Errors] = error.Errors;
      }

      return pd;
   }
}