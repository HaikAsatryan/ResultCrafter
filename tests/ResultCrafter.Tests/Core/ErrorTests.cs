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

    [Fact]
    public void BadRequest_WithNullMessageArray_ThrowsArgumentException()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["field"] = null!
        };

        Assert.Throws<ArgumentException>(() => Error.BadRequest(errors));
    }

    [Fact]
    public void BadRequest_WithEmptyDictionary_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Error.BadRequest(new Dictionary<string, string[]>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void BadRequest_WithNullOrWhitespaceDetail_Throws(string? detail)
    {
        // The prose overload is the only reason a plain BadRequest carries; without it the client gets nothing.
        Assert.ThrowsAny<ArgumentException>(() => Error.BadRequest(detail!));
    }

    [Fact]
    public void BadRequest_WithNullDictionary_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Error.BadRequest((IReadOnlyDictionary<string, string[]>)null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BadRequest_WithEmptyOrWhitespaceFieldKey_ThrowsArgumentException(string key)
    {
        // A blank key would become an "errors" member the client cannot tie to a field.
        Assert.Throws<ArgumentException>(() => Error.BadRequest(new Dictionary<string, string[]> { [key] = ["m"] }));
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
    public void Equality_ValidationErrorsWithDifferentFieldOrMessage_AreNotEqual()
    {
        var a = Error.BadRequest(new Dictionary<string, string[]>
        {
            ["email"] = ["Required."]
        });

        // Same number of fields, so only the per-field comparison can tell them apart.
        var otherField = Error.BadRequest(new Dictionary<string, string[]>
        {
            ["name"] = ["Required."]
        });
        var otherMessage = Error.BadRequest(new Dictionary<string, string[]>
        {
            ["email"] = ["Invalid."]
        });

        Assert.NotEqual(a, otherField);
        Assert.NotEqual(a, otherMessage);
    }

    [Fact]
    public void Equality_ErrorsOrExtensionsOfDifferentShape_AreNotEqual()
    {
        var email = new Dictionary<string, string[]> { ["email"] = ["Required."] };
        var emailAndName = new Dictionary<string, string[]> { ["email"] = ["Required."], ["name"] = ["Required."] };

        // Each pair is checked in both directions: the smaller side's entries all match the larger side, so only
        // the count and null checks tell them apart.
        AssertNotEqualBothWays(Error.BadRequest(email, "invalid"), Error.BadRequest(emailAndName, "invalid"));
        AssertNotEqualBothWays(Error.BadRequest("invalid"), Error.BadRequest(email, "invalid"));
        AssertNotEqualBothWays(Error.Conflict(), Error.Conflict().With("code", 1));
        AssertNotEqualBothWays(Error.Conflict().With("code", 1), Error.Conflict().With("other", 1));
    }

    [Fact]
    public void Equality_ObjectOverload_ComparesValues()
    {
        // object.Equals is what non-generic collections and object-typed comparisons use.
        object boxed = Error.NotFound("x");

        Assert.True(Error.NotFound("x").Equals(boxed));
        Assert.False(Error.NotFound("x").Equals((object)"x"));
    }

    private static void AssertNotEqualBothWays(Error a, Error b)
    {
        Assert.False(a.Equals(b));
        Assert.False(b.Equals(a));
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
    public void DefaultErrorType_IsNone()
    {
        Assert.Equal(ErrorType.None, default);
    }

    // ── With: extension members ────────────────────────────────────────────────

    [Theory]
    [InlineData("type")]
    [InlineData("Title")]
    [InlineData("STATUS")]
    [InlineData("detail")]
    [InlineData("instance")]
    [InlineData("errors")]
    [InlineData("traceid")]
    [InlineData("requestId")]
    [InlineData("x-rc")]
    [InlineData("x-rc-error-id")]
    [InlineData("X-RC-RETRY-AFTER")]
    [InlineData("")]
    [InlineData(" ")]
    public void With_ReservedOrEmptyKey_Throws(string key)
    {
        Assert.ThrowsAny<ArgumentException>(() => Error.Conflict().With(key, "value"));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void With_NonFiniteDouble_Throws(double value)
    {
        // JSON has no representation for these, so the error response could not be written.
        Assert.Throws<ArgumentOutOfRangeException>(() => Error.Conflict().With("ratio", value));
    }

    [Fact]
    public void With_ReturnsNewError_SameKeyReplaces_OriginalUnchanged()
    {
        var original = Error.Conflict("taken");

        var updated = original.With("code", 1).With("code", 2);

        Assert.Empty(original.Extensions);
        Assert.Equal(2, Assert.Single(updated.Extensions).Value);
        Assert.Equal("taken", updated.Detail);
    }

    [Fact]
    public void Equality_IncludesExtensionsAndRetryAfter()
    {
        var a = Error.Conflict("taken").With("tags", ["a", "b"]).With("limit", 5);
        var sameContent = Error.Conflict("taken").With("limit", 5).With("tags", ["a", "b"]);

        Assert.Equal(a, sameContent);
        Assert.True(a == sameContent);
        Assert.Equal(a.GetHashCode(), sameContent.GetHashCode());
        Assert.NotEqual(a, Error.Conflict("taken").With("tags", ["a", "c"]).With("limit", 5));
        Assert.NotEqual(Error.TooManyRequests(retryAfter: TimeSpan.FromSeconds(1)),
            Error.TooManyRequests(retryAfter: TimeSpan.FromSeconds(2)));
    }

    // ── TooManyRequests: Retry-After ───────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TooManyRequests_NonPositiveRetryAfter_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Error.TooManyRequests(retryAfter: TimeSpan.FromSeconds(seconds)));
    }

    // ── Copies, null guards and operators ──────────────────────────────────────

    [Fact]
    public void With_CopiesArray_AndKeepsErrorsAndRetryAfter()
    {
        var tags = new[] { "a" };
        var error = Error.Conflict().With("tags", tags);
        tags[0] = "mutated";
        Assert.Equal(["a"], (string[])error.Extensions["tags"]);

        var wait = TimeSpan.FromSeconds(5);
        Assert.Equal(wait, Error.TooManyRequests(retryAfter: wait).With("k", 1).RetryAfter);
        Assert.NotNull(Error.BadRequest(new Dictionary<string, string[]> { ["f"] = ["m"] }).With("k", 1).Errors);
    }

    [Fact]
    public void With_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Error.Conflict().With("k", (string)null!));
        Assert.Throws<ArgumentNullException>(() => Error.Conflict().With("k", (string[])null!));
        Assert.Throws<ArgumentNullException>(() => Error.Conflict().With(null!, 1));
    }

    [Fact]
    public void Operators_HandleNull()
    {
        Error? none = null;
        Error? alsoNone = null;

        Assert.False(Error.NotFound() == none);
        Assert.True(Error.NotFound() != none);
        Assert.True(none == alsoNone);
    }

    [Fact]
    public void IsValidation_NarrowsErrorsToNonNull()
    {
        var error = Error.BadRequest(new Dictionary<string, string[]> { ["f"] = ["m"] });

        // Compiles without a null-forgiving operator because of [MemberNotNullWhen].
        if (error.IsValidation) Assert.Single(error.Errors);
    }

    [Fact]
    public void TooManyRequests_RetryAfterAboveIntMaxSeconds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Error.TooManyRequests(retryAfter: TimeSpan.MaxValue));
        Assert.NotNull(Error.TooManyRequests(retryAfter: TimeSpan.FromSeconds(int.MaxValue)).RetryAfter);
    }

    // ── BadRequest with FieldError values ─────────────────────────────────────

    [Fact]
    public void BadRequest_WithFieldErrors_SetsMessagesAndFieldErrors()
    {
        var taken = new FieldError("Email is taken.", "email_in_use");
        var tooLong = new FieldError("Name is too long.", "too_long").With("maxLength", 100);

        var error = Error.BadRequest(new Dictionary<string, FieldError[]>
        {
            ["email"] = [taken],
            ["name"] = [tooLong]
        }, "invalid");

        Assert.True(error.IsValidation);
        Assert.Equal(ErrorType.BadRequest, error.Type);
        Assert.Equal("invalid", error.Detail);
        Assert.Equal(["Email is taken."], error.Errors!["email"]);
        Assert.Equal(["Name is too long."], error.Errors["name"]);
        Assert.Equal([taken], error.FieldErrors!["email"]);
        Assert.Equal([tooLong], error.FieldErrors["name"]);
    }

    [Fact]
    public void BadRequest_WithFieldErrors_MergesKeysThatDifferByCase_InBothDictionaries()
    {
        var error = Error.BadRequest(new Dictionary<string, FieldError[]>
        {
            ["Email"] = [new FieldError("Required.", "NotEmptyValidator")],
            ["email"] = [new FieldError("Invalid.", "EmailValidator")]
        });

        var (key, messages) = Assert.Single(error.Errors!);
        Assert.Equal("Email", key);
        Assert.Equal(["Required.", "Invalid."], messages);
        Assert.Equal(["NotEmptyValidator", "EmailValidator"], error.FieldErrors!["email"].Select(e => e.Code).ToArray());
    }

    [Fact]
    public void BadRequest_WithFieldErrors_CopiesDictionaryAndArrays()
    {
        var errors = new Dictionary<string, FieldError[]> { ["email"] = [new FieldError("Required.")] };

        var error = Error.BadRequest(errors);
        errors["email"][0] = new FieldError("Mutated!");
        errors["new"] = [new FieldError("Injected!")];

        Assert.False(error.FieldErrors!.ContainsKey("new"));
        Assert.Equal("Required.", error.FieldErrors["email"][0].Message);
    }

    [Fact]
    public void BadRequest_WithFieldErrors_RejectsInvalidInput()
    {
        Assert.Throws<ArgumentNullException>(() => Error.BadRequest((IReadOnlyDictionary<string, FieldError[]>)null!));
        Assert.Throws<ArgumentException>(() => Error.BadRequest(new Dictionary<string, FieldError[]>()));
        Assert.Throws<ArgumentException>(() =>
            Error.BadRequest(new Dictionary<string, FieldError[]> { [" "] = [new FieldError("m")] }));
        Assert.Throws<ArgumentException>(() =>
            Error.BadRequest(new Dictionary<string, FieldError[]> { ["f"] = null! }));
        Assert.Throws<ArgumentException>(() =>
            Error.BadRequest(new Dictionary<string, FieldError[]> { ["f"] = [null!] }));
    }

    [Fact]
    public void BadRequest_WithMessages_HasNoFieldErrors()
    {
        Assert.Null(Error.BadRequest(new Dictionary<string, string[]> { ["f"] = ["m"] }).FieldErrors);
        Assert.Null(Error.BadRequest("prose").FieldErrors);
    }

    [Fact]
    public void Equality_MessagesOnly_EqualsFieldErrorsWithoutCodesOrArgs()
    {
        var fromMessages = Error.BadRequest(new Dictionary<string, string[]> { ["email"] = ["Required."] });
        var fromFieldErrors = Error.BadRequest(new Dictionary<string, FieldError[]>
        {
            ["email"] = [new FieldError("Required.")]
        });

        Assert.Equal(fromMessages, fromFieldErrors);
        Assert.Equal(fromFieldErrors, fromMessages);
        Assert.Equal(fromMessages.GetHashCode(), fromFieldErrors.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentCodeOrArgs_AreNotEqual()
    {
        static Error Build(FieldError fieldError)
        {
            return Error.BadRequest(new Dictionary<string, FieldError[]> { ["name"] = [fieldError] });
        }

        var coded = Build(new FieldError("Too long.", "too_long").With("maxLength", 100));

        Assert.Equal(coded, Build(new FieldError("Too long.", "too_long").With("maxLength", 100)));
        AssertNotEqualBothWays(coded, Build(new FieldError("Too long.", "other").With("maxLength", 100)));
        AssertNotEqualBothWays(coded, Build(new FieldError("Too long.", "too_long").With("maxLength", 50)));
        AssertNotEqualBothWays(coded, Error.BadRequest(new Dictionary<string, string[]> { ["name"] = ["Too long."] }));
        AssertNotEqualBothWays(Build(new FieldError("Too long.").With("maxLength", 100)),
            Error.BadRequest(new Dictionary<string, string[]> { ["name"] = ["Too long."] }));
    }

    [Fact]
    public void With_KeepsFieldErrors()
    {
        var error = Error.BadRequest(new Dictionary<string, FieldError[]>
        {
            ["email"] = [new FieldError("Required.", "NotEmptyValidator")]
        });

        var extended = error.With("hint", "x");

        Assert.Same(error.FieldErrors, extended.FieldErrors);
        Assert.Same(error.Errors, extended.Errors);
    }

    [Fact]
    public void With_DateTime_KeepsTheDateTime()
    {
        var at = new DateTime(2026, 11, 10, 12, 30, 0, DateTimeKind.Utc);

        var error = Error.Conflict().With("at", at);

        Assert.Equal(at, Assert.IsType<DateTime>(error.Extensions["at"]));
    }
}
