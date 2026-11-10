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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Created_NullOrWhitespaceLocation_Throws(string? location)
    {
        Assert.ThrowsAny<ArgumentException>(() => Result<string>.Created(location!, "hello"));
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

        Assert.Equal(error, result.Error);
        Assert.Equal(SuccessKind.None, result.Kind);
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
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
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

    [Fact]
    public void Equality_DifferingInOneMember_AreNotEqual()
    {
        // Each pair matches in every member but one, so each member's comparison has to count on its own.
        Assert.NotEqual(Result<int>.Ok(1), Result<int>.Ok(2));
        Assert.NotEqual(Result<int>.Ok(1), Result<int>.Accepted(1));
        Assert.NotEqual(Result<int>.Created("/a", 1), Result<int>.Created("/b", 1));
        Assert.NotEqual(Result<int>.Fail(Error.NotFound()), Result<int>.Fail(Error.Conflict()));
    }

    [Fact]
    public void Equality_ObjectOverloadAndInequalityOperator()
    {
        var one = Result<int>.Ok(1);
        var two = Result<int>.Ok(2);
        object boxedOne = Result<int>.Ok(1);

        Assert.True(one.Equals(boxedOne));
        Assert.False(one.Equals((object)1));
        Assert.True(one != two);
        Assert.False(one != Result<int>.Ok(1));
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
        Assert.Equal(error, mapped.Error);
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

    [Fact]
    public void Map_SelectorReturnsNull_Throws()
    {
        // A success Result<T> never carries null, so Map cannot produce one either.
        var result = Result<int>.Ok(42);
        Assert.Throws<InvalidOperationException>(() => result.Map<string>(_ => null!));
    }

    [Fact]
    public void Equality_FailWithEqualButDistinctErrors_AreEqual()
    {
        Assert.Equal(Result<int>.Fail(Error.NotFound("x")), Result<int>.Fail(Error.NotFound("x")));
        Assert.Equal(Result<int>.Fail(Error.NotFound("x")).GetHashCode(),
            Result<int>.Fail(Error.NotFound("x")).GetHashCode());
        Assert.True(Result<int>.Fail(Error.NotFound("x")) == Result<int>.Fail(Error.NotFound("x")));
    }

    [Fact]
    public void NullValue_Throws_ForEveryFactoryAndConversion()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Created("/items/1", null!));
        Assert.Throws<ArgumentNullException>(() => Result<string>.Accepted(null!));
        Assert.Throws<ArgumentNullException>(() =>
        {
            Result<string> result = (string)null!;
            return result;
        });
    }

    [Fact]
    public void Size_MatchesDocs()
    {
        Assert.SkipUnless(IntPtr.Size == 8, "The documented sizes are for 64-bit.");
        Assert.Equal(32, System.Runtime.CompilerServices.Unsafe.SizeOf<Result<string>>());
    }
}
