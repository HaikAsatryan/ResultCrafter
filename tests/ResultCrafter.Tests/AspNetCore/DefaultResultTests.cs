using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using ResultCrafter.AspNetCore;
using ControllerExtensions = ResultCrafter.AspNetCore.Mvc.ControllerResultExtensions;
using MinimalApi = ResultCrafter.AspNetCore.MinimalApiResultExtensions;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     A <c>default</c> result bypasses every factory, so it is neither a success nor a failure.
///     Reading it as one must fail loudly instead of sending a 200.
/// </summary>
public sealed class DefaultResultTests
{
    [Theory]
    [InlineData("Result<string>")]
    [InlineData("Result<int>")]
    [InlineData("Result")]
    public void DefaultInstance_GuardedMembersThrow_DiagnosticsDoNot(string shape)
    {
        var (guarded, factoryName, diagnostics) = Shape(shape);

        foreach (var (member, call) in guarded)
        {
            var ex = Assert.Throws<InvalidOperationException>(call);
            Assert.True(ex.Message.Contains("default", StringComparison.Ordinal) &&
                        ex.Message.Contains(factoryName, StringComparison.Ordinal) &&
                        ex.Message.Contains("Fail", StringComparison.Ordinal),
                $"{shape}.{member} threw the wrong message: {ex.Message}");
        }

        foreach (var call in diagnostics) call();
    }

    [Fact]
    public async Task DefaultResultOfInt_ToOkResult_Returns500Not200()
    {
        await using var app = TestApp.Build(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/default", () => default(Result<int>).ToOkResult());
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/default", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static (List<(string Member, Action Call)> Guarded, string FactoryName, List<Action> Diagnostics) Shape(
        string shape)
    {
        return shape switch
        {
            // Map is passed in from non-generic code: the .NET 11 validation generator treats any
            // Map(delegate) call as an endpoint and warns (ASP0034) when the parameter type is generic.
            "Result<string>" => ForResultOfT("x", () => default(Result<string>).Map(v => v)),
            "Result<int>" => ForResultOfT(1, () => default(Result<int>).Map(v => v)),
            "Result" => ForVoidResult(),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
        };
    }

    private static (List<(string, Action)>, string, List<Action>) ForResultOfT<T>(T sample, Action map)
        where T : notnull
    {
        var d = default(Result<T>);
        var guarded = new List<(string, Action)>
        {
            ("IsSuccess", () => _ = d.IsSuccess),
            ("Map", map),
            ("Match", () => d.Match(_ => 0, _ => 0)),
            ("MinimalApi.ToOkResult", () => MinimalApi.ToOkResult(d)),
            ("MinimalApi.ToCreatedResult", () => MinimalApi.ToCreatedResult(d)),
            ("MinimalApi.ToAcceptedResult", () => MinimalApi.ToAcceptedResult(d)),
            ("Mvc.ToOkResult", () => ControllerExtensions.ToOkResult(d)),
            ("Mvc.ToCreatedResult", () => ControllerExtensions.ToCreatedResult(d)),
            ("Mvc.ToAcceptedResult", () => ControllerExtensions.ToAcceptedResult(d))
        };
        var diagnostics = new List<Action>
        {
            () => _ = d.ToString(),
            () => _ = DebuggerDisplay(d),
            () => _ = d.Equals(default),
            () => _ = d.Equals(Result<T>.Ok(sample)),
            () => _ = d == default,
            () => _ = d.GetHashCode()
        };
        return (guarded, "Ok", diagnostics);
    }

    private static (List<(string, Action)>, string, List<Action>) ForVoidResult()
    {
        var d = default(Result);
        var guarded = new List<(string, Action)>
        {
            ("IsSuccess", () => _ = d.IsSuccess),
            ("Match", () => d.Match(() => 0, _ => 0)),
            ("MinimalApi.ToNoContentResult", () => MinimalApi.ToNoContentResult(d)),
            ("MinimalApi.ToAcceptedResult", () => MinimalApi.ToAcceptedResult(d)),
            ("Mvc.ToNoContentResult", () => ControllerExtensions.ToNoContentResult(d)),
            ("Mvc.ToAcceptedResult", () => ControllerExtensions.ToAcceptedResult(d))
        };
        var diagnostics = new List<Action>
        {
            () => _ = d.ToString(),
            () => _ = DebuggerDisplay(d),
            () => _ = d.Equals(default),
            () => _ = d.Equals(Result.NoContent()),
            () => _ = d == default,
            () => _ = d.GetHashCode()
        };
        return (guarded, "NoContent", diagnostics);
    }

    // The debugger evaluates this private property; it must not throw on a default instance.
    private static object? DebuggerDisplay(object instance)
    {
        return instance.GetType()
            .GetProperty("DebuggerDisplay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);
    }
}
