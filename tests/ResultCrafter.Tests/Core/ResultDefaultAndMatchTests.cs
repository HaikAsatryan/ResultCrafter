using ResultCrafter.AspNetCore;
using ResultCrafter.AspNetCore.Mvc;

namespace ResultCrafter.Tests.Core;

/// <summary>
///     Covers the null-<see cref="Error" /> guards, the <c>Match</c> combinators, and the case-insensitive key
///     merge in <see cref="Error.BadRequest(IReadOnlyDictionary{string, string[]}, string?)" />. Default
///     results are covered by <c>DefaultResultTests</c>.
/// </summary>
public sealed class ResultDefaultAndMatchTests
{
    // ── A null Error never becomes a failure result ────────────────────────────

    [Theory]
    [InlineData("Result.Fail")]
    [InlineData("Result<T>.Fail")]
    [InlineData("implicit Result")]
    [InlineData("implicit Result<T>")]
    [InlineData("MinimalApi.ToProblemResult")]
    [InlineData("Mvc.ToProblemResult")]
    public void NullError_ThrowsArgumentNull(string entryPoint)
    {
        Error error = null!;
        Action call = entryPoint switch
        {
            "Result.Fail" => () => Result.Fail(error),
            "Result<T>.Fail" => () => Result<int>.Fail(error),
            "implicit Result" => () => _ = (Result)error,
            "implicit Result<T>" => () => _ = (Result<int>)error,
            "MinimalApi.ToProblemResult" => () => MinimalApiResultExtensions.ToProblemResult(error),
            "Mvc.ToProblemResult" => () => ControllerResultExtensions.ToProblemResult(error),
            _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
        };

        Assert.Throws<ArgumentNullException>(call);
    }

    // ── Match: void Result ─────────────────────────────────────────────────────

    [Fact]
    public void Match_VoidSuccess_InvokesOnSuccess()
    {
        var result = Result.NoContent();

        var outcome = result.Match(() => "success", _ => "failure");

        Assert.Equal("success", outcome);
    }

    [Fact]
    public void Match_VoidFailure_InvokesOnFailureWithError()
    {
        var error = Error.Conflict("already_exists");
        var result = Result.Fail(error);

        var outcome = result.Match(() => "success", e => e.Detail!);

        Assert.Equal("already_exists", outcome);
    }

    [Fact]
    public void Match_Void_NullOnSuccess_ThrowsArgumentNull()
    {
        var result = Result.NoContent();

        Assert.Throws<ArgumentNullException>(() => result.Match(null!, _ => 0));
    }

    [Fact]
    public void Match_Void_NullOnFailure_ThrowsArgumentNull()
    {
        var result = Result.NoContent();

        Assert.Throws<ArgumentNullException>(() => result.Match(() => 0, null!));
    }

    // ── Match: Result<T> ───────────────────────────────────────────────────────

    [Fact]
    public void Match_OfTSuccess_ReceivesValue()
    {
        var result = Result<int>.Ok(42);

        var outcome = result.Match(v => v * 2, _ => -1);

        Assert.Equal(84, outcome);
    }

    [Fact]
    public void Match_OfTFailure_ReceivesError()
    {
        var error = Error.NotFound("missing");
        var result = Result<int>.Fail(error);

        var outcome = result.Match(v => v.ToString(), e => e.Detail!);

        Assert.Equal("missing", outcome);
    }

    [Fact]
    public void Match_OfT_NullOnSuccess_ThrowsArgumentNull()
    {
        var result = Result<int>.Ok(1);

        Assert.Throws<ArgumentNullException>(() => result.Match(null!, _ => 0));
    }

    [Fact]
    public void Match_OfT_NullOnFailure_ThrowsArgumentNull()
    {
        var result = Result<int>.Ok(1);

        Assert.Throws<ArgumentNullException>(() => result.Match(v => v, null!));
    }

    // ── Error.BadRequest: keys differing only by case merge their messages ─────

    [Fact]
    public void BadRequest_KeysDifferingOnlyByCase_MergesBothMessageArrays()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = ["Email is required."],
            ["Email"] = ["Email must be valid."]
        };

        var error = Error.BadRequest(errors);

        var entry = Assert.Single(error.Errors!);
        Assert.Equal(2, entry.Value.Length);
        Assert.Contains("Email is required.", entry.Value);
        Assert.Contains("Email must be valid.", entry.Value);
    }

    [Fact]
    public void BadRequest_MergedKeys_RemainCaseInsensitivelyRetrievable()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = ["Required."],
            ["EMAIL"] = ["Invalid."]
        };

        var error = Error.BadRequest(errors);

        Assert.True(error.Errors!.TryGetValue("Email", out var messages));
        Assert.Equal(2, messages.Length);
    }
}
