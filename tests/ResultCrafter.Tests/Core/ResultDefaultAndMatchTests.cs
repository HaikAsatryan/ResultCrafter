using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.Core;

/// <summary>
///     Pins the CLR-default semantics of the result structs, documents how
///     <c>default(Error)</c> smuggles the <see cref="ErrorType.None" /> sentinel past the
///     factory guards (and where the catalog catches it), covers the <c>Match</c>
///     combinators, and guards the case-insensitive key merge in
///     <see cref="Error.BadRequest(Dictionary{string, string[]}, string?)" />.
/// </summary>
public sealed class ResultDefaultAndMatchTests
{
    // ── default(Result) / default(Result<T>) semantics ─────────────────────────

    [Fact]
    public void DefaultVoidResult_IsSuccessWithOkKindAndNullError()
    {
        var result = default(Result);

        Assert.True(result.IsSuccess);
        Assert.Equal(SuccessKind.Ok, result.Kind);
        Assert.Null(result.Error);
        Assert.Null(result.AcceptedLocation);
    }

    [Fact]
    public void DefaultResultOfT_IsSuccessWithOkKindAndNullError()
    {
        var result = default(Result<string>);

        Assert.True(result.IsSuccess);
        Assert.Equal(SuccessKind.Ok, result.Kind);
        Assert.Null(result.Error);
        Assert.Null(result.Value);
        Assert.Null(result.Location);
    }

    // ── default(Error) smuggles the None sentinel past the factory guards ──────

    [Fact]
    public void Fail_DefaultError_SmugglesErrorTypeNone()
    {
        // default(Error) bypasses the private constructor guard, so the None sentinel
        // reaches the failure path. The catalog is the backstop that rejects it.
        var result = Result.Fail(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.Error!.Value.Type);
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.Status(result.Error.Value.Type));
    }

    [Fact]
    public void FailOfT_DefaultError_SmugglesErrorTypeNone()
    {
        var result = Result<int>.Fail(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.None, result.Error!.Value.Type);
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.Status(result.Error.Value.Type));
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
