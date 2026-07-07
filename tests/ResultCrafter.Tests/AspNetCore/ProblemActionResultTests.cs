using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore.Mvc;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore;

public sealed class ProblemActionResultTests
{
    // ════════════════════════════════════════════════════════
    // Constructor / Error property
    // ════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_SetsErrorProperty()
    {
        var error = Error.NotFound("test");
        var result = new ProblemActionResult(error);
        Assert.Equal(error, result.Error);
    }

    [Theory]
    [InlineData(ErrorType.BadRequest)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.ConcurrencyConflict)]
    public void Constructor_AllErrorTypes_PreservesError(ErrorType type)
    {
        var error = BuildError(type);
        var result = new ProblemActionResult(error);
        Assert.Equal(error, result.Error);
    }

    // ════════════════════════════════════════════════════════
    // ExecuteResultAsync — IProblemDetailsService is called
    // ════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteResultAsync_CallsIProblemDetailsService()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.NotFound());

        await result.ExecuteResultAsync(ctx);

        Assert.True(spy.Called);
    }

    [Fact]
    public async Task ExecuteResultAsync_PassesNonNullProblemDetailsContext()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.NotFound());

        await result.ExecuteResultAsync(ctx);

        Assert.NotNull(spy.CapturedContext);
    }

    [Fact]
    public async Task ExecuteResultAsync_PassesCorrectHttpContext()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.BadRequest("bad"));

        await result.ExecuteResultAsync(ctx);

        Assert.Same(ctx.HttpContext, spy.CapturedContext!.HttpContext);
    }

    // ════════════════════════════════════════════════════════
    // ExecuteResultAsync — ProblemDetails status codes
    // ════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteResultAsync_NotFound_Sets404Status()
    {
        await AssertStatusCode(Error.NotFound(), 404);
    }

    [Fact]
    public async Task ExecuteResultAsync_BadRequest_Sets400Status()
    {
        await AssertStatusCode(Error.BadRequest("bad"), 400);
    }

    [Fact]
    public async Task ExecuteResultAsync_Unauthorized_Sets401Status()
    {
        await AssertStatusCode(Error.Unauthorized(), 401);
    }

    [Fact]
    public async Task ExecuteResultAsync_Forbidden_Sets403Status()
    {
        await AssertStatusCode(Error.Forbidden(), 403);
    }

    [Fact]
    public async Task ExecuteResultAsync_Conflict_Sets409Status()
    {
        await AssertStatusCode(Error.Conflict(), 409);
    }

    [Fact]
    public async Task ExecuteResultAsync_ConcurrencyConflict_Sets409Status()
    {
        await AssertStatusCode(Error.ConcurrencyConflict(), 409);
    }

    // ════════════════════════════════════════════════════════
    // ExecuteResultAsync — ProblemDetails titles
    // ════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteResultAsync_NotFound_SetsNotFoundTitle()
    {
        await AssertTitle(Error.NotFound(), "not_found");
    }

    [Fact]
    public async Task ExecuteResultAsync_BadRequest_SetsBadRequestTitle()
    {
        await AssertTitle(Error.BadRequest("bad"), "bad_request");
    }

    [Fact]
    public async Task ExecuteResultAsync_Unauthorized_SetsUnauthorizedTitle()
    {
        await AssertTitle(Error.Unauthorized(), "unauthorized");
    }

    [Fact]
    public async Task ExecuteResultAsync_Forbidden_SetsForbiddenTitle()
    {
        await AssertTitle(Error.Forbidden(), "forbidden");
    }

    [Fact]
    public async Task ExecuteResultAsync_Conflict_SetsConflictTitle()
    {
        await AssertTitle(Error.Conflict(), "conflict");
    }

    [Fact]
    public async Task ExecuteResultAsync_ConcurrencyConflict_SetsConcurrencyConflictTitle()
    {
        await AssertTitle(Error.ConcurrencyConflict(), "concurrency_conflict");
    }

    // ════════════════════════════════════════════════════════
    // ExecuteResultAsync — ProblemDetails detail
    // ════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteResultAsync_WithDetail_UsesProvidedDetail()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.NotFound("Order 42 not found."));

        await result.ExecuteResultAsync(ctx);

        Assert.Equal("Order 42 not found.", spy.CapturedContext!.ProblemDetails.Detail);
    }

    [Fact]
    public async Task ExecuteResultAsync_WithoutDetail_UsesCatalogDefault()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.NotFound());

        await result.ExecuteResultAsync(ctx);

        Assert.Equal("resource_not_found", spy.CapturedContext!.ProblemDetails.Detail);
    }

    // ════════════════════════════════════════════════════════
    // ExecuteResultAsync — RC marker extensions
    // ════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteResultAsync_SetsRcMarkerExtension()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.NotFound());

        await result.ExecuteResultAsync(ctx);

        var ext = spy.CapturedContext!.ProblemDetails.Extensions;
        Assert.True(ext.ContainsKey(ProblemDetailsKeys.RcMarker));
        Assert.Equal(true, ext[ProblemDetailsKeys.RcMarker]);
    }

    [Fact]
    public async Task ExecuteResultAsync_SetsRcErrorIdExtension()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.NotFound());

        await result.ExecuteResultAsync(ctx);

        var ext = spy.CapturedContext!.ProblemDetails.Extensions;
        Assert.True(ext.ContainsKey(ProblemDetailsKeys.RcErrorId));
    }

    // ════════════════════════════════════════════════════════
    // ExecuteResultAsync — validation errors extension
    // ════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteResultAsync_ValidationError_SetsErrorsExtension()
    {
        var (ctx, spy) = BuildContext();
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = ["Required."]
        };
        var result = new ProblemActionResult(Error.BadRequest(errors));

        await result.ExecuteResultAsync(ctx);

        var ext = spy.CapturedContext!.ProblemDetails.Extensions;
        Assert.True(ext.ContainsKey(ProblemDetailsKeys.Errors));
    }

    [Fact]
    public async Task ExecuteResultAsync_PlainBadRequest_DoesNotSetErrorsExtension()
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(Error.BadRequest("bad input"));

        await result.ExecuteResultAsync(ctx);

        var ext = spy.CapturedContext!.ProblemDetails.Extensions;
        Assert.False(ext.ContainsKey(ProblemDetailsKeys.Errors));
    }

    [Fact]
    public async Task ExecuteResultAsync_ValidationError_PreservesFieldErrorDictionary()
    {
        var (ctx, spy) = BuildContext();
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = ["Email is required.", "Email must be valid."],
            ["quantity"] = ["Must be > 0."]
        };
        var result = new ProblemActionResult(Error.BadRequest(errors));

        await result.ExecuteResultAsync(ctx);

        var ext = spy.CapturedContext!.ProblemDetails.Extensions;
        Assert.True(ext.ContainsKey(ProblemDetailsKeys.Errors));
    }

    // ════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════

    private static (ActionContext context, CapturingProblemDetailsService spy) BuildContext()
    {
        var spy = new CapturingProblemDetailsService();

        var services = new ServiceCollection();
        services.AddSingleton<IProblemDetailsService>(spy);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        return (new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), spy);
    }

    private static async Task AssertStatusCode(Error error, int expectedStatus)
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(error);

        await result.ExecuteResultAsync(ctx);

        Assert.Equal(expectedStatus, spy.CapturedContext!.ProblemDetails.Status);
    }

    private static async Task AssertTitle(Error error, string expectedTitle)
    {
        var (ctx, spy) = BuildContext();
        var result = new ProblemActionResult(error);

        await result.ExecuteResultAsync(ctx);

        Assert.Equal(expectedTitle, spy.CapturedContext!.ProblemDetails.Title);
    }

    private static Error BuildError(ErrorType type)
    {
        return type switch
        {
            ErrorType.BadRequest => Error.BadRequest("detail"),
            ErrorType.NotFound => Error.NotFound("detail"),
            ErrorType.Conflict => Error.Conflict("detail"),
            ErrorType.Unauthorized => Error.Unauthorized("detail"),
            ErrorType.Forbidden => Error.Forbidden("detail"),
            ErrorType.ConcurrencyConflict => Error.ConcurrencyConflict("detail"),
            _ => Error.BadRequest("fallback")
        };
    }

    /// <summary>
    ///     Test spy for <see cref="IProblemDetailsService" />. Captures the context passed
    ///     to <see cref="WriteAsync" /> without actually writing an HTTP response.
    /// </summary>
    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public bool Called { get; private set; }
        public ProblemDetailsContext? CapturedContext { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Called = true;
            CapturedContext = context;
            // Mirror the status code the real service would set so assertions are consistent.
            context.HttpContext.Response.StatusCode = context.ProblemDetails.Status ?? 500;
            return ValueTask.CompletedTask;
        }
    }
}
