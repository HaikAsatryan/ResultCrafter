using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     A <see cref="Result{T}" />, <see cref="Result" /> or <see cref="Error" /> returned straight from an
///     endpoint used to be serialized as JSON with 200, even on failure. The app's HTTP JSON options now
///     refuse these types, so the mistake surfaces as a 500 that names the fix.
/// </summary>
public sealed class UnmappedResultTests
{
    [Fact]
    public async Task MinimalApi_ReturnsFailedResultDirectly_FailsNamingTheFix()
    {
        await using var app = BuildApp(false, a =>
            a.MapGet("/unmapped", () => Result<UnmappedDto>.Fail(Error.Conflict("email_taken"))));

        await AssertEndpointBuildFails(app, "/unmapped");
    }

    // Error and the void Result are refused on their own, not only as part of a Result<T>.
    [Theory]
    [InlineData("error")]
    [InlineData("void-result")]
    public async Task MinimalApi_ReturnsErrorOrVoidResultDirectly_FailsNamingTheFix(string kind)
    {
        await using var app = BuildApp(false, a =>
        {
            if (kind == "error") a.MapGet("/unmapped", () => Error.Conflict("email_taken"));
            else a.MapGet("/unmapped", () => Result.Fail(Error.Conflict("email_taken")));
        });

        await AssertEndpointBuildFails(app, "/unmapped");
    }

    [Fact]
    public async Task Controller_ReturnsFailedResultDirectly_Returns500NamingTheFix()
    {
        await using var app = BuildApp(true, a => a.MapControllers());

        await AssertUnmapped500(app, "/unmapped-controller");
    }

    [Fact]
    public void JsonSerializer_WithOwnOptions_StillSerializesResultAndError()
    {
        // Code that serializes these types with its own options (a cache, a message bus) is not affected.
        var json = JsonSerializer.Serialize(Result<UnmappedDto>.Fail(Error.Conflict("email_taken")),
            JsonSerializerOptions.Web);

        Assert.Contains("email_taken", json, StringComparison.Ordinal);
    }

#if NET11_0_OR_GREATER
    [Fact]
    public async Task MinimalApi_ReturnsUnionWithErrorCaseDirectly_FailsNamingTheFix()
    {
        await using var app = BuildApp(false, a =>
            a.MapGet("/union", () => (CreateUserResult)Error.Conflict("email_taken")));

        await AssertEndpointBuildFails(app, "/union");
    }
#endif

    private static WebApplication BuildApp(bool controllers, Action<WebApplication> map)
    {
        return TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter(o => o.ExceptionDetailMode = ExceptionDetailMode.IncludeExceptionDetails);
                if (controllers)
                    b.Services.AddControllers().AddApplicationPart(typeof(UnmappedResultController).Assembly);
            },
            a =>
            {
                a.UseResultCrafter();
                map(a);
            });
    }

    // A Minimal API endpoint that returns a Result throws while the endpoint is built. With authorization
    // services registered (AddControllers registers them), WebApplication builds every endpoint at startup and
    // StartAsync throws. Without them the build runs on the first request, inside the routing middleware that
    // WebApplication places before UseResultCrafter; Kestrel answers that with a bare 500, TestServer rethrows.
    private static async Task AssertEndpointBuildFails(WebApplication app, string path)
    {
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync(path, TestContext.Current.CancellationToken));

        Assert.Contains("ToOkResult()", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ToProblemResult()", ex.Message, StringComparison.Ordinal);
    }

    private static async Task AssertUnmapped500(WebApplication app, string path)
    {
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var detail = doc.RootElement.GetProperty("detail").GetString();
        Assert.Contains("ToOkResult()", detail, StringComparison.Ordinal);
        Assert.Contains("ToProblemResult()", detail, StringComparison.Ordinal);
    }
}

public sealed record UnmappedDto(int Id);

#if NET11_0_OR_GREATER
public readonly union CreateUserResult(UnmappedDto, Error);
#endif

/// <summary>Returns a <see cref="Result{T}" /> without mapping it, which is the mistake under test.</summary>
[ApiController]
[Route("unmapped-controller")]
public sealed class UnmappedResultController : ControllerBase
{
    /// <summary>Returns a failed result directly.</summary>
    [HttpGet]
    public Result<UnmappedDto> Get()
    {
        return Error.Conflict("email_taken");
    }
}
