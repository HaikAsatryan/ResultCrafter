using ResultCrafter.AspNetCore.ProblemDetails;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class ProblemDetailsBuilderTests
{
   [Fact]
   public void Build_SetsCorrectStatusAndTitle()
   {
      var pd = ProblemDetailsBuilder.Build(Error.NotFound("Item not found."));

      Assert.Equal(404, pd.Status);
      Assert.Equal("not_found", pd.Title);
   }

   [Fact]
   public void Build_SetsTypeUri()
   {
      var pd = ProblemDetailsBuilder.Build(Error.NotFound());

      Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", pd.Type);
   }

   [Fact]
   public void Build_SetsDetailFromError()
   {
      var pd = ProblemDetailsBuilder.Build(Error.NotFound("Item 42 does not exist."));

      Assert.Equal("Item 42 does not exist.", pd.Detail);
   }

   [Fact]
   public void Build_FallsBackToCatalogDetailWhenErrorHasNoDetail()
   {
      var pd = ProblemDetailsBuilder.Build(Error.NotFound());

      Assert.Equal("resource_not_found", pd.Detail);
   }

   [Fact]
   public void Build_SetsRcMarkerExtension()
   {
      var pd = ProblemDetailsBuilder.Build(Error.NotFound());

      Assert.True(pd.Extensions.ContainsKey(ProblemDetailsKeys.RcMarker));
      Assert.Equal(true, pd.Extensions[ProblemDetailsKeys.RcMarker]);
   }

   [Fact]
   public void Build_SetsRcErrorIdExtension()
   {
      var pd = ProblemDetailsBuilder.Build(Error.NotFound());

      Assert.True(pd.Extensions.ContainsKey(ProblemDetailsKeys.RcErrorId));
      Assert.Equal("NotFound", pd.Extensions[ProblemDetailsKeys.RcErrorId]);
   }

   [Fact]
   public void Build_ValidationError_SetsErrorsExtension()
   {
      var errors = new Dictionary<string, string[]>
      {
         ["email"] = ["Email is required."]
      };
      var pd = ProblemDetailsBuilder.Build(Error.BadRequest(errors));

      Assert.True(pd.Extensions.ContainsKey(ProblemDetailsKeys.Errors));
   }

   [Fact]
   public void Build_NonValidationError_DoesNotSetErrorsExtension()
   {
      var pd = ProblemDetailsBuilder.Build(Error.BadRequest("Something is wrong."));

      Assert.False(pd.Extensions.ContainsKey(ProblemDetailsKeys.Errors));
   }

   [Fact]
   public void Build_AllErrorTypes_ProduceNonNullResult()
   {
      // Regression guard: no ErrorType should cause a null reference or unmatched switch
      var types = Enum.GetValues<ErrorType>();

      foreach (var type in types)
      {
         var error = type switch
         {
            ErrorType.BadRequest => Error.BadRequest("detail"),
            ErrorType.NotFound => Error.NotFound(),
            ErrorType.Conflict => Error.Conflict(),
            ErrorType.Unauthorized => Error.Unauthorized(),
            ErrorType.Forbidden => Error.Forbidden(),
            ErrorType.ConcurrencyConflict => Error.ConcurrencyConflict(),
            _ => Error.BadRequest("fallback")
         };

         var pd = ProblemDetailsBuilder.Build(error);

         Assert.NotNull(pd);
         Assert.NotNull(pd.Title);
         Assert.True(pd.Status > 0, $"Status should be set for {type}");
      }
   }
}