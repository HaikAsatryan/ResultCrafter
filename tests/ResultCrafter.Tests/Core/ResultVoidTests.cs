using ResultCrafter.Core.Primitives;

namespace ResultCrafter.Tests.Core;

public sealed class ResultVoidTests
{
   // ── NoContent ─────────────────────────────────────────────────────────────

   [Fact]
   public void NoContent_IsSuccess_IsTrue()
   {
      var result = Result.NoContent();
      Assert.True(result.IsSuccess);
   }

   [Fact]
   public void NoContent_ErrorIsNull()
   {
      var result = Result.NoContent();
      Assert.Null(result.Error);
   }

   [Fact]
   public void NoContent_AcceptedLocationIsNull()
   {
      var result = Result.NoContent();
      Assert.Null(result.AcceptedLocation);
   }

   // ── Accepted ──────────────────────────────────────────────────────────────

   [Fact]
   public void Accepted_IsSuccess_IsTrue()
   {
      var result = Result.Accepted();
      Assert.True(result.IsSuccess);
   }

   [Fact]
   public void Accepted_WithLocation_SetsAcceptedLocation()
   {
      var result = Result.Accepted("/api/jobs/42/status");
      Assert.Equal("/api/jobs/42/status", result.AcceptedLocation);
   }

   [Fact]
   public void Accepted_WithoutLocation_AcceptedLocationIsNull()
   {
      var result = Result.Accepted();
      Assert.Null(result.AcceptedLocation);
   }

   // ── Fail ──────────────────────────────────────────────────────────────────

   [Fact]
   public void Fail_IsSuccess_IsFalse()
   {
      var result = Result.Fail(Error.NotFound());
      Assert.False(result.IsSuccess);
   }

   [Fact]
   public void Fail_CarriesError()
   {
      var error = Error.Forbidden("Access denied.");
      var result = Result.Fail(error);

      Assert.Equal(error, result.Error!.Value);
   }

   // ── Implicit operator ─────────────────────────────────────────────────────

   [Fact]
   public void ImplicitFromError_ProducesFailResult()
   {
      Result result = Error.NotFound("Not found.");

      Assert.False(result.IsSuccess);
      Assert.Equal(ErrorType.NotFound, result.Error!.Value.Type);
   }

   // ── SuccessKind ─────────────────────────────────────────────────────────

   [Fact]
   public void NoContent_KindIsNoContent()
   {
      var result = Result.NoContent();
      Assert.Equal(SuccessKind.NoContent, result.Kind);
   }

   [Fact]
   public void Accepted_KindIsAccepted()
   {
      var result = Result.Accepted();
      Assert.Equal(SuccessKind.Accepted, result.Kind);
   }

   [Fact]
   public void Fail_KindIsOk()
   {
      var result = Result.Fail(Error.NotFound());
      Assert.Equal(SuccessKind.Ok, result.Kind);
   }

   // ── Equality ──────────────────────────────────────────────────────────────

   [Fact]
   public void Equality_TwoNoContentResults_AreEqual()
   {
      var a = Result.NoContent();
      var b = Result.NoContent();

      Assert.Equal(a, b);
   }

   [Fact]
   public void Equality_NoContentAndFail_AreNotEqual()
   {
      var a = Result.NoContent();
      var b = Result.Fail(Error.NotFound());

      Assert.NotEqual(a, b);
   }

   [Fact]
   public void Equality_NoContentAndAccepted_AreNotEqual()
   {
      var a = Result.NoContent();
      var b = Result.Accepted();

      Assert.NotEqual(a, b);
   }
}