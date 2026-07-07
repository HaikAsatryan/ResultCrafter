using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class HttpErrorCatalogTests
{
    // ── Status codes ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ErrorType.BadRequest, 400)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.ConcurrencyConflict, 409)]
    [InlineData(ErrorType.Gone, 410)]
    [InlineData(ErrorType.UnprocessableEntity, 422)]
    [InlineData(ErrorType.TooManyRequests, 429)]
    public void Status_ReturnsCorrectHttpStatusCode(ErrorType type, int expectedStatus)
    {
        Assert.Equal(expectedStatus, HttpErrorCatalog.Status(type));
    }

    // ── Titles ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ErrorType.BadRequest, "bad_request")]
    [InlineData(ErrorType.Unauthorized, "unauthorized")]
    [InlineData(ErrorType.Forbidden, "forbidden")]
    [InlineData(ErrorType.NotFound, "not_found")]
    [InlineData(ErrorType.Conflict, "conflict")]
    [InlineData(ErrorType.ConcurrencyConflict, "concurrency_conflict")]
    [InlineData(ErrorType.Gone, "gone")]
    [InlineData(ErrorType.UnprocessableEntity, "unprocessable_entity")]
    [InlineData(ErrorType.TooManyRequests, "too_many_requests")]
    public void Title_ReturnsCorrectTitle(ErrorType type, string expectedTitle)
    {
        Assert.Equal(expectedTitle, HttpErrorCatalog.Title(type));
    }

    // ── Detail resolution ─────────────────────────────────────────────────────

    [Fact]
    public void ResolveDetail_WhenErrorHasDetail_ReturnsErrorDetail()
    {
        var error = Error.NotFound("Order 42 does not exist.");
        Assert.Equal("Order 42 does not exist.", HttpErrorCatalog.ResolveDetail(error));
    }

    [Fact]
    public void ResolveDetail_WhenErrorHasNoDetail_ReturnsCatalogDefault()
    {
        var error = Error.NotFound();
        Assert.Equal("resource_not_found", HttpErrorCatalog.ResolveDetail(error));
    }

    [Fact]
    public void ResolveDetail_BadRequestWithNoDetail_ReturnsCatalogDefault()
    {
        // BadRequest via dictionary has no explicit detail
        var error = Error.BadRequest(new Dictionary<string, string[]>
        {
            ["name"] = ["Required."]
        });
        Assert.Equal("the_request_was_invalid_or_cannot_be_otherwise_served",
            HttpErrorCatalog.ResolveDetail(error));
    }

    // ── Type URIs ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ErrorType.BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1")]
    [InlineData(ErrorType.Unauthorized, "https://tools.ietf.org/html/rfc9110#section-15.5.2")]
    [InlineData(ErrorType.Forbidden, "https://tools.ietf.org/html/rfc9110#section-15.5.4")]
    [InlineData(ErrorType.NotFound, "https://tools.ietf.org/html/rfc9110#section-15.5.5")]
    [InlineData(ErrorType.Conflict, "https://tools.ietf.org/html/rfc9110#section-15.5.10")]
    [InlineData(ErrorType.ConcurrencyConflict, "https://tools.ietf.org/html/rfc9110#section-15.5.10")]
    [InlineData(ErrorType.Gone, "https://tools.ietf.org/html/rfc9110#section-15.5.11")]
    [InlineData(ErrorType.UnprocessableEntity, "https://tools.ietf.org/html/rfc9110#section-15.5.21")]
    [InlineData(ErrorType.TooManyRequests, "https://tools.ietf.org/html/rfc6585#section-4")]
    public void TypeUri_ReturnsCorrectRfc9110Uri(ErrorType type, string expectedUri)
    {
        Assert.Equal(expectedUri, HttpErrorCatalog.TypeUri(type));
    }

    // ── ErrorType.None throws ─────────────────────────────────────────────────

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
    public void TypeUri_None_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpErrorCatalog.TypeUri(ErrorType.None));
    }
}
