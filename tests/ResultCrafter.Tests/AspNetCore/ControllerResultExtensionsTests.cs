using Microsoft.AspNetCore.Mvc;
using ResultCrafter.AspNetCore.Controllers;
using ResultCrafter.Core.Primitives;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class ControllerResultExtensionsTests
{
   // ════════════════════════════════════════════════════════
   // ToOkResult<T>
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToOkResult_Success_ReturnsOkObjectResult()
   {
      var result = Result<string>.Ok("hello");
      var action = result.ToOkResult();
      Assert.IsType<OkObjectResult>(action.Result);
   }

   [Fact]
   public void ToOkResult_Success_PreservesValue()
   {
      var result = Result<string>.Ok("hello");
      var action = result.ToOkResult();
      var ok = Assert.IsType<OkObjectResult>(action.Result);
      Assert.Equal("hello", ok.Value);
   }

   [Fact]
   public void ToOkResult_SuccessWithNullValue_ReturnsOkObjectResultWithNull()
   {
      var result = Result<string?>.Ok(null);
      var action = result.ToOkResult();
      var ok = Assert.IsType<OkObjectResult>(action.Result);
      Assert.Null(ok.Value);
   }

   [Fact]
   public void ToOkResult_Failure_ReturnsProblemActionResult()
   {
      var result = Result<string>.Fail(Error.NotFound("item missing"));
      var action = result.ToOkResult();
      Assert.IsType<ProblemActionResult>(action.Result);
   }

   [Theory]
   [InlineData(ErrorType.NotFound)]
   [InlineData(ErrorType.BadRequest)]
   [InlineData(ErrorType.Unauthorized)]
   [InlineData(ErrorType.Forbidden)]
   [InlineData(ErrorType.Conflict)]
   [InlineData(ErrorType.ConcurrencyConflict)]
   public void ToOkResult_AnyErrorType_ReturnsProblemActionResult(ErrorType type)
   {
      var error = BuildError(type);
      var result = Result<int>.Fail(error);
      var action = result.ToOkResult();
      Assert.IsType<ProblemActionResult>(action.Result);
   }

   [Fact]
   public void ToOkResult_Failure_ProblemActionResultCarriesError()
   {
      var error = Error.NotFound("gone");
      var result = Result<string>.Fail(error);
      var action = result.ToOkResult();
      var problem = Assert.IsType<ProblemActionResult>(action.Result);
      Assert.Equal(error, problem.Error);
   }

   [Fact]
   public void ToOkResult_ImplicitValueConversion_Works()
   {
      // Ensures implicit T → Result<T> conversion composes with the extension
      Result<int> result = 42;
      var action = result.ToOkResult();
      var ok = Assert.IsType<OkObjectResult>(action.Result);
      Assert.Equal(42, ok.Value);
   }

   // ════════════════════════════════════════════════════════
   // ToCreatedResult<T>
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToCreatedResult_Success_ReturnsCreatedResult()
   {
      var result = Result<string>.Created("/api/items/1", "item");
      var action = result.ToCreatedResult();
      Assert.IsType<CreatedResult>(action.Result);
   }

   [Fact]
   public void ToCreatedResult_Success_SetsLocationHeader()
   {
      var result = Result<string>.Created("/api/items/42", "item");
      var action = result.ToCreatedResult();
      var created = Assert.IsType<CreatedResult>(action.Result);
      Assert.Equal("/api/items/42", created.Location);
   }

   [Fact]
   public void ToCreatedResult_Success_PreservesValue()
   {
      var result = Result<string>.Created("/api/items/1", "my item");
      var action = result.ToCreatedResult();
      var created = Assert.IsType<CreatedResult>(action.Result);
      Assert.Equal("my item", created.Value);
   }

   [Fact]
   public void ToCreatedResult_Failure_ReturnsProblemActionResult()
   {
      var result = Result<string>.Fail(Error.BadRequest("bad"));
      var action = result.ToCreatedResult();
      Assert.IsType<ProblemActionResult>(action.Result);
   }

   [Fact]
   public void ToCreatedResult_WhenKindIsOk_Throws()
   {
      var result = Result<string>.Ok("value");
      Assert.Throws<InvalidOperationException>(() => result.ToCreatedResult());
   }

   [Fact]
   public void ToCreatedResult_WhenKindIsAccepted_Throws()
   {
      var result = Result<string>.Accepted("value");
      Assert.Throws<InvalidOperationException>(() => result.ToCreatedResult());
   }

   [Fact]
   public void ToCreatedResult_ThrowMessage_MentionsCreatedFactory()
   {
      var result = Result<string>.Ok("value");
      var ex = Assert.Throws<InvalidOperationException>(() => result.ToCreatedResult());
      Assert.Contains("Result<T>.Created", ex.Message);
   }

   // ════════════════════════════════════════════════════════
   // ToAcceptedResult<T> (typed)
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToAcceptedResult_Typed_Success_ReturnsAcceptedResult()
   {
      var result = Result<string>.Accepted("item", "/api/items/1/status");
      var action = result.ToAcceptedResult();
      Assert.IsType<AcceptedResult>(action.Result);
   }

   [Fact]
   public void ToAcceptedResult_Typed_Success_SetsLocation()
   {
      var result = Result<string>.Accepted("item", "/api/items/1/status");
      var action = result.ToAcceptedResult();
      var accepted = Assert.IsType<AcceptedResult>(action.Result);
      Assert.Equal("/api/items/1/status", accepted.Location);
   }

   [Fact]
   public void ToAcceptedResult_Typed_Success_PreservesValue()
   {
      var result = Result<string>.Accepted("payload");
      var action = result.ToAcceptedResult();
      var accepted = Assert.IsType<AcceptedResult>(action.Result);
      Assert.Equal("payload", accepted.Value);
   }

   [Fact]
   public void ToAcceptedResult_Typed_SuccessWithoutLocation_LocationIsNull()
   {
      var result = Result<string>.Accepted("payload");
      var action = result.ToAcceptedResult();
      var accepted = Assert.IsType<AcceptedResult>(action.Result);
      Assert.Null(accepted.Location);
   }

   [Fact]
   public void ToAcceptedResult_Typed_Failure_ReturnsProblemActionResult()
   {
      var result = Result<string>.Fail(Error.NotFound());
      var action = result.ToAcceptedResult();
      Assert.IsType<ProblemActionResult>(action.Result);
   }

   [Fact]
   public void ToAcceptedResult_Typed_Failure_CarriesError()
   {
      var error = Error.Forbidden("no access");
      var result = Result<string>.Fail(error);
      var action = result.ToAcceptedResult();
      var problem = Assert.IsType<ProblemActionResult>(action.Result);
      Assert.Equal(error, problem.Error);
   }

   // ════════════════════════════════════════════════════════
   // ToNoContentResult (void Result)
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToNoContentResult_Success_ReturnsNoContentResult()
   {
      var result = Result.NoContent();
      Assert.IsType<NoContentResult>(result.ToNoContentResult());
   }

   [Fact]
   public void ToNoContentResult_Failure_ReturnsProblemActionResult()
   {
      var result = Result.Fail(Error.NotFound("gone"));
      Assert.IsType<ProblemActionResult>(result.ToNoContentResult());
   }

   [Theory]
   [InlineData(ErrorType.NotFound)]
   [InlineData(ErrorType.BadRequest)]
   [InlineData(ErrorType.Unauthorized)]
   [InlineData(ErrorType.Forbidden)]
   [InlineData(ErrorType.Conflict)]
   [InlineData(ErrorType.ConcurrencyConflict)]
   public void ToNoContentResult_AnyErrorType_ReturnsProblemActionResult(ErrorType type)
   {
      var result = Result.Fail(BuildError(type));
      Assert.IsType<ProblemActionResult>(result.ToNoContentResult());
   }

   [Fact]
   public void ToNoContentResult_Failure_CarriesCorrectError()
   {
      var error = Error.NotFound("not here");
      var result = Result.Fail(error);
      var action = result.ToNoContentResult();
      var problem = Assert.IsType<ProblemActionResult>(action);
      Assert.Equal(error, problem.Error);
   }

   // ════════════════════════════════════════════════════════
   // ToAcceptedResult (void Result)
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToAcceptedResult_Void_Success_ReturnsAcceptedResult()
   {
      var result = Result.Accepted();
      Assert.IsType<AcceptedResult>(result.ToAcceptedResult());
   }

   [Fact]
   public void ToAcceptedResult_Void_SuccessWithLocation_SetsLocation()
   {
      var result = Result.Accepted("/api/jobs/123");
      var action = result.ToAcceptedResult();
      var accepted = Assert.IsType<AcceptedResult>(action);
      Assert.Equal("/api/jobs/123", accepted.Location);
   }

   [Fact]
   public void ToAcceptedResult_Void_SuccessWithoutLocation_LocationIsNull()
   {
      var result = Result.Accepted();
      var action = result.ToAcceptedResult();
      var accepted = Assert.IsType<AcceptedResult>(action);
      Assert.Null(accepted.Location);
   }

   [Fact]
   public void ToAcceptedResult_Void_Failure_ReturnsProblemActionResult()
   {
      var result = Result.Fail(Error.BadRequest("bad input"));
      Assert.IsType<ProblemActionResult>(result.ToAcceptedResult());
   }

   [Fact]
   public void ToAcceptedResult_Void_Failure_CarriesCorrectError()
   {
      var error = Error.BadRequest("bad input");
      var result = Result.Fail(error);
      var action = result.ToAcceptedResult();
      var problem = Assert.IsType<ProblemActionResult>(action);
      Assert.Equal(error, problem.Error);
   }

   // ════════════════════════════════════════════════════════
   // ToProblemResult (bare Error)
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToProblemResult_ReturnsProblemActionResult()
   {
      var action = Error.NotFound()
                        .ToProblemResult();
      Assert.IsType<ProblemActionResult>(action);
   }

   [Theory]
   [InlineData(ErrorType.NotFound)]
   [InlineData(ErrorType.BadRequest)]
   [InlineData(ErrorType.Unauthorized)]
   [InlineData(ErrorType.Forbidden)]
   [InlineData(ErrorType.Conflict)]
   [InlineData(ErrorType.ConcurrencyConflict)]
   public void ToProblemResult_AnyErrorType_ReturnsProblemActionResult(ErrorType type)
   {
      var action = BuildError(type)
         .ToProblemResult();
      Assert.IsType<ProblemActionResult>(action);
   }

   [Fact]
   public void ToProblemResult_CarriesOriginalError()
   {
      var error = Error.NotFound("specific detail");
      var action = error.ToProblemResult();
      var problem = Assert.IsType<ProblemActionResult>(action);
      Assert.Equal(error, problem.Error);
   }

   [Fact]
   public void ToProblemResult_ValidationError_CarriesFieldErrors()
   {
      var errors = new Dictionary<string, string[]>
      {
         ["email"] = ["Required."]
      };
      var error = Error.BadRequest(errors);
      var action = error.ToProblemResult();
      var problem = Assert.IsType<ProblemActionResult>(action);
      Assert.True(problem.Error.IsValidation);
   }

   // ════════════════════════════════════════════════════════
   // Implicit conversion interop
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ImplicitErrorConversion_ComposesWithToOkResult()
   {
      // Ensures that implicit Error → Result<T> → .ToOkResult() pipeline is sound
      Result<string> result = Error.NotFound("x");
      var action = result.ToOkResult();
      Assert.IsType<ProblemActionResult>(action.Result);
   }

   [Fact]
   public void ImplicitErrorConversion_ComposesWithToNoContentResult()
   {
      Result result = Error.Forbidden("x");
      Assert.IsType<ProblemActionResult>(result.ToNoContentResult());
   }

   // ════════════════════════════════════════════════════════
   // Void Result SuccessKind mismatch throws
   // ════════════════════════════════════════════════════════

   [Fact]
   public void ToNoContentResult_WhenKindIsAccepted_Throws()
   {
      var result = Result.Accepted();
      var ex = Assert.Throws<InvalidOperationException>(() => result.ToNoContentResult());
      Assert.Contains("Result.NoContent()", ex.Message);
   }

   [Fact]
   public void ToAcceptedResult_WhenKindIsNoContent_Throws()
   {
      var result = Result.NoContent();
      var ex = Assert.Throws<InvalidOperationException>(() => result.ToAcceptedResult());
      Assert.Contains("Result.Accepted()", ex.Message);
   }

   // ── Helpers ────────────────────────────────────────────────────────────────

   private static Error BuildError(ErrorType type)
   {
      return type switch
      {
         ErrorType.BadRequest => Error.BadRequest("detail"),
         ErrorType.NotFound => Error.NotFound("detail"),
         ErrorType.Conflict => Error.Conflict("detail"),
         ErrorType.Unauthorized => Error.Unauthorized("detail"),
         ErrorType.Forbidden => Error.Forbidden("detail"),
         ErrorType.ConcurrencyConflict => Error.ConcurrencyConflict("detail"),
         _ => Error.BadRequest("fallback")
      };
   }
}