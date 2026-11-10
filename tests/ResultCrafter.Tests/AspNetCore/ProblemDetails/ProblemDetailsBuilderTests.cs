using System.Text.Json;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore.ProblemDetails;

public sealed class ProblemDetailsBuilderTests
{
    [Fact]
    public void Build_NullError_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ProblemDetailsBuilder.Build(null!));
    }

    [Fact]
    public void Build_SetsCorrectStatusAndTitle()
    {
        var pd = ProblemDetailsBuilder.Build(Error.NotFound("Item not found."));

        Assert.Equal(404, pd.Status);
        Assert.Equal("not_found", pd.Title);
    }

    [Fact]
    public void Build_SetsTypeUri()
    {
        var pd = ProblemDetailsBuilder.Build(Error.NotFound());

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5", pd.Type);
    }

    [Fact]
    public void Build_SetsDetailFromError()
    {
        var pd = ProblemDetailsBuilder.Build(Error.NotFound("Item 42 does not exist."));

        Assert.Equal("Item 42 does not exist.", pd.Detail);
    }

    [Fact]
    public void Build_FallsBackToCatalogDetailWhenErrorHasNoDetail()
    {
        var pd = ProblemDetailsBuilder.Build(Error.NotFound());

        Assert.Equal("resource_not_found", pd.Detail);
    }

    [Fact]
    public void Build_SetsRcMarkerExtension()
    {
        var pd = ProblemDetailsBuilder.Build(Error.NotFound());

        Assert.True(pd.Extensions.ContainsKey(ProblemDetailsKeys.RcMarker));
        Assert.Equal(true, pd.Extensions[ProblemDetailsKeys.RcMarker]);
    }

    [Fact]
    public void Build_SetsRcErrorIdExtension()
    {
        var pd = ProblemDetailsBuilder.Build(Error.NotFound());

        Assert.True(pd.Extensions.ContainsKey(ProblemDetailsKeys.RcErrorId));
        Assert.Equal("NotFound", pd.Extensions[ProblemDetailsKeys.RcErrorId]);
    }

    [Fact]
    public void Build_ValidationError_SetsErrorsExtension()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = ["Email is required."]
        };
        var pd = ProblemDetailsBuilder.Build(Error.BadRequest(errors));

        Assert.True(pd.Extensions.ContainsKey(ProblemDetailsKeys.Errors));
    }

    [Fact]
    public void Build_NonValidationError_DoesNotSetErrorsExtension()
    {
        var pd = ProblemDetailsBuilder.Build(Error.BadRequest("Something is wrong."));

        Assert.False(pd.Extensions.ContainsKey(ProblemDetailsKeys.Errors));
    }

    [Fact]
    public void Build_AllErrorTypes_ProduceNonNullResult()
    {
        // Regression guard: no ErrorType should cause a null reference or unmatched switch
        var types = Enum.GetValues<ErrorType>();

        foreach (var type in types)
        {
            // None is the sentinel: rejected by every factory and by the catalog.
            if (type == ErrorType.None) continue;

            var error = type switch
            {
                ErrorType.BadRequest => Error.BadRequest("detail"),
                ErrorType.NotFound => Error.NotFound(),
                ErrorType.Conflict => Error.Conflict(),
                ErrorType.Unauthorized => Error.Unauthorized(),
                ErrorType.Forbidden => Error.Forbidden(),
                ErrorType.ConcurrencyConflict => Error.ConcurrencyConflict(),
                ErrorType.Gone => Error.Gone(),
                ErrorType.UnprocessableEntity => Error.UnprocessableEntity(),
                ErrorType.TooManyRequests => Error.TooManyRequests(),
                _ => throw new InvalidOperationException(
                    $"ErrorType.{type} has no factory case in this test; add one so it is actually covered.")
            };

            var pd = ProblemDetailsBuilder.Build(error);

            Assert.NotNull(pd);
            Assert.NotNull(pd.Title);
            Assert.True(pd.Status > 0, $"Status should be set for {type}");
        }
    }

    // ── Error.With values become JSON elements ─────────────────────────────────
    // JsonElement is one of the few extension value types ProblemDetails can write with reflection-based
    // JSON off (NativeAOT); the AOT smoke app proves that part.

    [Theory]
    [InlineData("string", "\"abc\"")]
    [InlineData("int", "42")]
    [InlineData("long", "9223372036854775807")]
    [InlineData("bool", "true")]
    [InlineData("double", "1.5")]
    [InlineData("decimal", "19.90")]
    [InlineData("Guid", "\"6f9619ff-8b86-d011-b42d-00c04fc964ff\"")]
    [InlineData("DateTimeOffset", "\"2026-11-10T12:30:00+04:00\"")]
    [InlineData("DateTime", "\"2026-11-10T12:30:00Z\"")]
    [InlineData("string[]", "[\"a\",\"b\"]")]
    public void Build_WithExtension_WritesJsonValue(string valueType, string expectedJson)
    {
        var error = Error.Conflict();
        error = valueType switch
        {
            "string" => error.With("value", "abc"),
            "int" => error.With("value", 42),
            "long" => error.With("value", long.MaxValue),
            "bool" => error.With("value", true),
            "double" => error.With("value", 1.5),
            "decimal" => error.With("value", 19.90m),
            "Guid" => error.With("value", Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
            "DateTimeOffset" => error.With("value", new DateTimeOffset(2026, 11, 10, 12, 30, 0, TimeSpan.FromHours(4))),
            "DateTime" => error.With("value", new DateTime(2026, 11, 10, 12, 30, 0, DateTimeKind.Utc)),
            "string[]" => error.With("value", ["a", "b"]),
            _ => throw new ArgumentOutOfRangeException(nameof(valueType))
        };

        var pd = ProblemDetailsBuilder.Build(error);

        var element = Assert.IsType<JsonElement>(pd.Extensions["value"]);
        Assert.Equal(expectedJson, element.GetRawText());
    }

    [Theory]
    [InlineData(600_000_000L, "60")]
    [InlineData(599_990_000L, "60")]
    [InlineData(1L, "1")]
    public void Build_RetryAfter_RoundsUpToWholeSeconds(long ticks, string expected)
    {
        var pd = ProblemDetailsBuilder.Build(Error.TooManyRequests(retryAfter: TimeSpan.FromTicks(ticks)));

        Assert.Equal(expected, pd.Extensions[ProblemDetailsKeys.RcRetryAfter]);
    }

    [Fact]
    public void Build_Errors_AreCopied_SoTheBodyCannotChangeTheError()
    {
        var error = Error.BadRequest(new Dictionary<string, string[]> { ["email"] = ["Required."] });
        var pd = ProblemDetailsBuilder.Build(error);

        var body = Assert.IsType<IReadOnlyDictionary<string, string[]>>(pd.Extensions[ProblemDetailsKeys.Errors],
            exactMatch: false);
        body["email"][0] = "Localized.";

        Assert.Equal("Required.", error.Errors!["email"][0]);
    }
}
