using ResultCrafter.Core.Primitives;

namespace ResultCrafter.Tests.Core;

public sealed class ResultOfTTests
{
   // ── Ok ────────────────────────────────────────────────────────────────────

   [Fact]
   public void Ok_IsSuccess_IsTrue()
   {
      var result = Result<string>.Ok("hello");
      Assert.True(result.IsSuccess);
   }

   [Fact]
   public void Ok_CarriesValue()
   {
      var result = Result<string>.Ok("hello");
      Assert.Equal("hello", result.Value);
   }

   [Fact]
   public void Ok_KindIsOk()
   {
      var result = Result<string>.Ok("hello");
      Assert.Equal(SuccessKind.Ok, result.Kind);
   }

   [Fact]
   public void Ok_ErrorIsNull()
   {
      var result = Result<string>.Ok("hello");
      Assert.Null(result.Error);
   }

   [Fact]
   public void Ok_LocationIsNull()
   {
      var result = Result<string>.Ok("hello");
      Assert.Null(result.Location);
   }

   // ── Created ───────────────────────────────────────────────────────────────

   [Fact]
   public void Created_IsSuccess_IsTrue()
   {
      var result = Result<string>.Created("/api/items/1", "hello");
      Assert.True(result.IsSuccess);
   }

   [Fact]
   public void Created_KindIsCreated()
   {
      var result = Result<string>.Created("/api/items/1", "hello");
      Assert.Equal(SuccessKind.Created, result.Kind);
   }

   [Fact]
   public void Created_SetsLocation()
   {
      var result = Result<string>.Created("/api/items/1", "hello");
      Assert.Equal("/api/items/1", result.Location);
   }

   [Fact]
   public void Created_CarriesValue()
   {
      var result = Result<string>.Created("/api/items/1", "hello");
      Assert.Equal("hello", result.Value);
   }

   // ── Accepted ──────────────────────────────────────────────────────────────

   [Fact]
   public void Accepted_IsSuccess_IsTrue()
   {
      var result = Result<string>.Accepted("hello");
      Assert.True(result.IsSuccess);
   }

   [Fact]
   public void Accepted_KindIsAccepted()
   {
      var result = Result<string>.Accepted("hello");
      Assert.Equal(SuccessKind.Accepted, result.Kind);
   }

   [Fact]
   public void Accepted_WithLocation_SetsLocation()
   {
      var result = Result<string>.Accepted("hello", "/api/items/1/status");
      Assert.Equal("/api/items/1/status", result.Location);
   }

   [Fact]
   public void Accepted_WithoutLocation_LocationIsNull()
   {
      var result = Result<string>.Accepted("hello");
      Assert.Null(result.Location);
   }

   // ── Fail ──────────────────────────────────────────────────────────────────

   [Fact]
   public void Fail_IsSuccess_IsFalse()
   {
      var result = Result<string>.Fail(Error.NotFound());
      Assert.False(result.IsSuccess);
   }

   [Fact]
   public void Fail_CarriesError()
   {
      var error = Error.NotFound("Item not found.");
      var result = Result<string>.Fail(error);

      Assert.Equal(error, result.Error!.Value);
   }

   [Fact]
   public void Fail_ValueIsDefault()
   {
      var result = Result<string>.Fail(Error.NotFound());
      Assert.Null(result.Value);
   }

   // ── Implicit operators ────────────────────────────────────────────────────

   [Fact]
   public void ImplicitFromValue_ProducesOkResult()
   {
      Result<string> result = "hello";

      Assert.True(result.IsSuccess);
      Assert.Equal("hello", result.Value);
      Assert.Equal(SuccessKind.Ok, result.Kind);
   }

   [Fact]
   public void ImplicitFromError_ProducesFailResult()
   {
      Result<string> result = Error.NotFound("Not found.");

      Assert.False(result.IsSuccess);
      Assert.Equal(ErrorType.NotFound, result.Error!.Value.Type);
   }

   // ── Equality ──────────────────────────────────────────────────────────────

   [Fact]
   public void Equality_TwoOkResultsWithSameValue_AreEqual()
   {
      var a = Result<string>.Ok("hello");
      var b = Result<string>.Ok("hello");

      Assert.Equal(a, b);
   }

   [Fact]
   public void Equality_OkAndFail_AreNotEqual()
   {
      var a = Result<string>.Ok("hello");
      var b = Result<string>.Fail(Error.NotFound());

      Assert.NotEqual(a, b);
   }

   // ── Map ──────────────────────────────────────────────────────────────────

   [Fact]
   public void Map_Success_TransformsValue()
   {
      var result = Result<int>.Ok(42);
      var mapped = result.Map(x => x.ToString());

      Assert.True(mapped.IsSuccess);
      Assert.Equal("42", mapped.Value);
   }

   [Fact]
   public void Map_Failure_PropagatesError()
   {
      var error = Error.NotFound("gone");
      var result = Result<int>.Fail(error);
      var mapped = result.Map(x => x.ToString());

      Assert.False(mapped.IsSuccess);
      Assert.Equal(error, mapped.Error!.Value);
   }

   [Fact]
   public void Map_Failure_DoesNotInvokeSelector()
   {
      var result = Result<int>.Fail(Error.NotFound());
      var invoked = false;

      result.Map(x =>
      {
         invoked = true;
         return x.ToString();
      });

      Assert.False(invoked);
   }

   [Fact]
   public void Map_PreservesKindAndLocation()
   {
      var result = Result<int>.Created("/api/items/1", 42);
      var mapped = result.Map(x => x * 2);

      Assert.Equal(SuccessKind.Created, mapped.Kind);
      Assert.Equal("/api/items/1", mapped.Location);
      Assert.Equal(84, mapped.Value);
   }

   [Fact]
   public void Map_NullSelector_Throws()
   {
      var result = Result<int>.Ok(42);
      Assert.Throws<ArgumentNullException>(() => result.Map<string>(null!));
   }
}