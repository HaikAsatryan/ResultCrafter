using ResultCrafter.Core.Primitives;

namespace ResultCrafter.Tests.Core;

public sealed class ErrorTests
{
   // ── Factory methods set the correct ErrorType ──────────────────────────────

   [Fact]
   public void NotFound_SetsCorrectType()
   {
      var error = Error.NotFound();
      Assert.Equal(ErrorType.NotFound, error.Type);
   }

   [Fact]
   public void Unauthorized_SetsCorrectType()
   {
      var error = Error.Unauthorized();
      Assert.Equal(ErrorType.Unauthorized, error.Type);
   }

   [Fact]
   public void Forbidden_SetsCorrectType()
   {
      var error = Error.Forbidden();
      Assert.Equal(ErrorType.Forbidden, error.Type);
   }

   [Fact]
   public void Conflict_SetsCorrectType()
   {
      var error = Error.Conflict();
      Assert.Equal(ErrorType.Conflict, error.Type);
   }

   [Fact]
   public void ConcurrencyConflict_SetsCorrectType()
   {
      var error = Error.ConcurrencyConflict();
      Assert.Equal(ErrorType.ConcurrencyConflict, error.Type);
   }

   // ── Detail message is carried through ─────────────────────────────────────

   [Fact]
   public void NotFound_WithDetail_SetsDetail()
   {
      var error = Error.NotFound("Item 42 does not exist.");
      Assert.Equal("Item 42 does not exist.", error.Detail);
   }

   [Fact]
   public void NotFound_WithoutDetail_DetailIsNull()
   {
      var error = Error.NotFound();
      Assert.Null(error.Detail);
   }

   // ── BadRequest(string) ─────────────────────────────────────────────────────

   [Fact]
   public void BadRequest_WithString_SetsTypeAndDetail()
   {
      var error = Error.BadRequest("At least one ID is required.");

      Assert.Equal(ErrorType.BadRequest, error.Type);
      Assert.Equal("At least one ID is required.", error.Detail);
   }

   [Fact]
   public void BadRequest_WithString_IsValidationIsFalse()
   {
      var error = Error.BadRequest("Something went wrong.");
      Assert.False(error.IsValidation);
   }

   [Fact]
   public void BadRequest_WithString_ErrorsIsNull()
   {
      var error = Error.BadRequest("Something went wrong.");
      Assert.Null(error.Errors);
   }

   // ── BadRequest(Dictionary) ─────────────────────────────────────────────────

   [Fact]
   public void BadRequest_WithDictionary_IsValidationIsTrue()
   {
      var errors = new Dictionary<string, string[]>
      {
         ["email"] = ["Email is required."]
      };

      var error = Error.BadRequest(errors);

      Assert.True(error.IsValidation);
   }

   [Fact]
   public void BadRequest_WithDictionary_SetsErrors()
   {
      var errors = new Dictionary<string, string[]>
      {
         ["email"] = ["Email is required.", "Email must be valid."],
         ["quantity"] = ["Quantity must be greater than 0."]
      };

      var error = Error.BadRequest(errors);

      Assert.NotNull(error.Errors);
      Assert.Equal(2, error.Errors.Count);
      Assert.Contains("Email is required.", error.Errors["email"]);
      Assert.Contains("Quantity must be greater than 0.", error.Errors["quantity"]);
   }

   [Fact]
   public void BadRequest_WithDictionary_OptionalDetailIsCarried()
   {
      var errors = new Dictionary<string, string[]>
      {
         ["name"] = ["Required."]
      };
      var error = Error.BadRequest(errors, "Validation failed.");

      Assert.Equal("Validation failed.", error.Detail);
   }

   // ── Equality ──────────────────────────────────────────────────────────────

   [Fact]
   public void Equality_SameTypeAndDetail_AreEqual()
   {
      var a = Error.NotFound("Not found.");
      var b = Error.NotFound("Not found.");

      Assert.Equal(a, b);
      Assert.True(a == b);
   }

   [Fact]
   public void Equality_DifferentDetail_AreNotEqual()
   {
      var a = Error.NotFound("Item 1 not found.");
      var b = Error.NotFound("Item 2 not found.");

      Assert.NotEqual(a, b);
      Assert.True(a != b);
   }

   [Fact]
   public void Equality_DifferentType_AreNotEqual()
   {
      var a = Error.NotFound();
      var b = Error.Conflict();

      Assert.NotEqual(a, b);
   }

   // ── ToString ───────────────────────────────────────────────────────────────

   [Fact]
   public void ToString_WithDetail_IncludesTypeAndDetail()
   {
      var error = Error.NotFound("Item not found.");
      Assert.Equal("NotFound: Item not found.", error.ToString());
   }

   [Fact]
   public void ToString_WithoutDetail_ReturnsTypeName()
   {
      var error = Error.NotFound();
      Assert.Equal("NotFound", error.ToString());
   }

   [Fact]
   public void Equality_ValidationErrorsWithSameContent_AreEqual()
   {
      var a = Error.BadRequest(new Dictionary<string, string[]>
      {
         ["Email"] = ["Required."]
      });

      var b = Error.BadRequest(new Dictionary<string, string[]>
      {
         ["email"] = ["Required."]
      });

      Assert.Equal(a, b);
      Assert.True(a == b);
      Assert.Equal(a.GetHashCode(), b.GetHashCode());
   }

   [Fact]
   public void BadRequest_WithDictionary_DefensivelyCopiesDictionaryAndArrays()
   {
      var errors = new Dictionary<string, string[]>
      {
         ["email"] = ["Required."]
      };

      var error = Error.BadRequest(errors);

      errors["email"][0] = "Mutated!";
      errors["new"] = ["Injected!"];

      Assert.False(error.Errors!.ContainsKey("new"));
      Assert.Equal("Required.", error.Errors["email"][0]);
   }

   // ── ErrorType.None guard ──────────────────────────────────────────────────

   [Fact]
   public void DefaultError_HasTypeNone()
   {
      var error = default(Error);
      Assert.Equal(ErrorType.None, error.Type);
   }

   [Fact]
   public void DefaultErrorType_IsNone()
   {
      Assert.Equal(ErrorType.None, default);
   }
}