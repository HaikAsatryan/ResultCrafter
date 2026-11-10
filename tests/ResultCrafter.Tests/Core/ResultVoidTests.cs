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
    public void NoContent_LocationIsNull()
    {
        var result = Result.NoContent();
        Assert.Null(result.Location);
    }

    // ── Accepted ──────────────────────────────────────────────────────────────

    [Fact]
    public void Accepted_IsSuccess_IsTrue()
    {
        var result = Result.Accepted();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Accepted_WithLocation_SetsLocation()
    {
        var result = Result.Accepted("/api/jobs/42/status");
        Assert.Equal("/api/jobs/42/status", result.Location);
    }

    [Fact]
    public void Accepted_WithoutLocation_LocationIsNull()
    {
        var result = Result.Accepted();
        Assert.Null(result.Location);
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

        Assert.Equal(error, result.Error);
    }

    // ── Implicit operator ─────────────────────────────────────────────────────

    [Fact]
    public void ImplicitFromError_ProducesFailResult()
    {
        Result result = Error.NotFound("Not found.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
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
    public void Fail_KindIsNone()
    {
        var result = Result.Fail(Error.NotFound());
        Assert.Equal(SuccessKind.None, result.Kind);
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

    [Fact]
    public void Equality_ObjectOverloadAndInequalityOperator()
    {
        var noContent = Result.NoContent();
        object boxedNoContent = Result.NoContent();

        Assert.True(noContent.Equals(boxedNoContent));
        Assert.False(noContent.Equals((object)"NoContent"));
        Assert.True(noContent != Result.Accepted());
        Assert.False(noContent != Result.NoContent());
    }

    [Fact]
    public void Equality_FailWithEqualButDistinctErrors_AreEqual()
    {
        Assert.Equal(Result.Fail(Error.NotFound("x")), Result.Fail(Error.NotFound("x")));
        Assert.Equal(Result.Fail(Error.NotFound("x")).GetHashCode(), Result.Fail(Error.NotFound("x")).GetHashCode());
        Assert.True(Result.Fail(Error.NotFound("x")) == Result.Fail(Error.NotFound("x")));
    }

    [Fact]
    public void Size_MatchesDocs()
    {
        Assert.SkipUnless(IntPtr.Size == 8, "The documented size is for 64-bit.");
        Assert.Equal(24, System.Runtime.CompilerServices.Unsafe.SizeOf<Result>());
    }
}
