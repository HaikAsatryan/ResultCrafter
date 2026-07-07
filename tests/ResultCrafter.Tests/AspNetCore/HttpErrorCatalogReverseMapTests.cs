using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     Guards the reverse status-to-<see cref="ErrorType" /> map used to normalize
///     framework-default ProblemDetails, the catalog default detail messages, and the
///     <see cref="ErrorType.None" /> sentinel rejection on all four catalog methods.
/// </summary>
public sealed class HttpErrorCatalogReverseMapTests
{
    // ── TryGetErrorType: mapped status codes ───────────────────────────────────

    [Theory]
    [InlineData(400, ErrorType.BadRequest)]
    [InlineData(401, ErrorType.Unauthorized)]
    [InlineData(403, ErrorType.Forbidden)]
    [InlineData(404, ErrorType.NotFound)]
    [InlineData(409, ErrorType.Conflict)]
    [InlineData(410, ErrorType.Gone)]
    [InlineData(422, ErrorType.UnprocessableEntity)]
    [InlineData(429, ErrorType.TooManyRequests)]
    public void TryGetErrorType_MappedStatus_ReturnsTrueWithExpectedType(int status, ErrorType expected)
    {
        var mapped = HttpErrorCatalog.TryGetErrorType(status, out var type);

        Assert.True(mapped);
        Assert.Equal(expected, type);
    }

    [Fact]
    public void TryGetErrorType_409_MapsToGenericConflict_NotConcurrencyConflict()
    {
        HttpErrorCatalog.TryGetErrorType(409, out var type);

        Assert.Equal(ErrorType.Conflict, type);
        Assert.NotEqual(ErrorType.ConcurrencyConflict, type);
    }

    // ── TryGetErrorType: unmapped status codes ─────────────────────────────────

    [Theory]
    [InlineData(200)]
    [InlineData(402)]
    [InlineData(418)]
    [InlineData(500)]
    public void TryGetErrorType_UnmappedStatus_ReturnsFalseWithNone(int status)
    {
        var mapped = HttpErrorCatalog.TryGetErrorType(status, out var type);

        Assert.False(mapped);
        Assert.Equal(ErrorType.None, type);
    }

    // ── DefaultDetail: every ErrorType ─────────────────────────────────────────

    [Theory]
    [InlineData(ErrorType.BadRequest, "the_request_was_invalid_or_cannot_be_otherwise_served")]
    [InlineData(ErrorType.NotFound, "resource_not_found")]
    [InlineData(ErrorType.Conflict, "conflict")]
    [InlineData(ErrorType.Unauthorized, "unauthorized")]
    [InlineData(ErrorType.Forbidden, "forbidden")]
    [InlineData(ErrorType.ConcurrencyConflict, "concurrency_conflict")]
    [InlineData(ErrorType.Gone, "resource_gone")]
    [InlineData(ErrorType.UnprocessableEntity, "the_request_was_well_formed_but_could_not_be_processed")]
    [InlineData(ErrorType.TooManyRequests, "too_many_requests")]
    public void DefaultDetail_EveryErrorType_ReturnsExpectedMessage(ErrorType type, string expected)
    {
        Assert.Equal(expected, HttpErrorCatalog.DefaultDetail(type));
    }

    // ── ErrorType.None sentinel: all four catalog methods reject it ───────────

    [Fact]
    public void Status_None_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.Status(ErrorType.None));
    }

    [Fact]
    public void Title_None_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.Title(ErrorType.None));
    }

    [Fact]
    public void DefaultDetail_None_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.DefaultDetail(ErrorType.None));
    }

    [Fact]
    public void TypeUri_None_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.TypeUri(ErrorType.None));
    }
}
