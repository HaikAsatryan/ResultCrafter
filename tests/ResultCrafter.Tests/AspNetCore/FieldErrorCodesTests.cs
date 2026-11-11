using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     A field error with a code reaches the client in <c>errorCodes</c>, next to an unchanged <c>errors</c>, so a
///     client can translate it. The member name and arg keys are fixed, whatever the app's JSON naming policy.
/// </summary>
public sealed class FieldErrorCodesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerFieldErrors_WriteErrorsAndErrorCodes(bool snakeCase)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                if (snakeCase)
                    b.Services.ConfigureHttpJsonOptions(o =>
                        o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapPost("/users", () => FieldErrorCodesController.UserErrors().ToProblemResult());
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsync("/users", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = doc.RootElement;

        Assert.Equal("An account with this email already exists.",
            root.GetProperty("errors").GetProperty("email")[0].GetString());
        Assert.Equal("""[{"code":"email_in_use"}]""", root.GetProperty("errorCodes").GetProperty("email").GetRawText());
        Assert.Equal("""[{"code":"too_long","args":{"maxLength":100}}]""",
            root.GetProperty("errorCodes").GetProperty("displayName").GetRawText());
    }

    [Fact]
    public async Task ControllerFieldErrors_WriteErrorCodes()
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Services.AddControllers().AddApplicationPart(typeof(FieldErrorCodesController).Assembly);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapControllers();
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsync("/field-error-codes", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errorCodes = doc.RootElement.GetProperty("errorCodes");
        Assert.Equal("""[{"code":"email_in_use"}]""", errorCodes.GetProperty("email").GetRawText());
        Assert.Equal("""[{"code":"too_long","args":{"maxLength":100}}]""",
            errorCodes.GetProperty("displayName").GetRawText());
    }

    [Fact]
    public async Task MessageOnlyFieldErrors_HaveNoErrorCodes()
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.MapPost("/users", () => Error.BadRequest(new Dictionary<string, string[]>
                {
                    ["email"] = ["Required."]
                }).ToProblemResult());
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.PostAsync("/users", null, TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(doc.RootElement.TryGetProperty("errors", out _));
        Assert.False(doc.RootElement.TryGetProperty("errorCodes", out _));
    }
}

/// <summary>Returns field errors with codes from a controller action.</summary>
[ApiController]
[Route("field-error-codes")]
public sealed class FieldErrorCodesController : ControllerBase
{
    internal static Error UserErrors()
    {
        return Error.BadRequest(new Dictionary<string, FieldError[]>
        {
            ["email"] = [new FieldError("An account with this email already exists.", "email_in_use")],
            ["displayName"] = [new FieldError("Display name is too long.", "too_long").With("maxLength", 100)]
        });
    }

    /// <summary>Always fails validation.</summary>
    [HttpPost]
    public IActionResult Post()
    {
        return ResultCrafter.AspNetCore.Mvc.ControllerResultExtensions.ToProblemResult(UserErrors());
    }
}
