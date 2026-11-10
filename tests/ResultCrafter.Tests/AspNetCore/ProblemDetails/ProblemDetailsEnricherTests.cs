using Microsoft.AspNetCore.Http;
using ResultCrafter.AspNetCore.ProblemDetails;

namespace ResultCrafter.Tests.AspNetCore.ProblemDetails;

public sealed class ProblemDetailsEnricherTests
{
    // ── GetInstance ────────────────────────────────────────────────────────────

    [Fact]
    public void GetInstance_ReturnsPathOnly()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/42");

        var instance = ProblemDetailsEnricher.GetInstance(ctx);

        Assert.Equal("/api/orders/42", instance);
    }

    [Fact]
    public void GetInstance_EscapesCharactersAUriCannotHold()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/a b");

        var instance = ProblemDetailsEnricher.GetInstance(ctx);

        Assert.Equal("/api/orders/a%20b", instance);
    }

    [Fact]
    public void GetInstance_DoesNotIncludeHttpMethod()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders", "POST");

        var instance = ProblemDetailsEnricher.GetInstance(ctx);

        Assert.DoesNotContain("POST", instance);
    }

    [Fact]
    public void GetInstance_CalledTwice_ReturnsSameReference()
    {
        // Verifies the result is cached in HttpContext.Items
        var ctx = BuildHttpContext("api.example.com", "/api/orders/1");

        var first = ProblemDetailsEnricher.GetInstance(ctx);
        var second = ProblemDetailsEnricher.GetInstance(ctx);

        Assert.Same(first, second);
    }

    // ── GetTraceId ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetTraceId_WhenNoActivity_ReturnsEmptyString()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders");

        var traceId = ProblemDetailsEnricher.GetTraceId(ctx);

        Assert.Equal(string.Empty, traceId);
    }

    [Fact]
    public void GetTraceId_CalledTwice_ReturnsSameReference()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders");

        var first = ProblemDetailsEnricher.GetTraceId(ctx);
        var second = ProblemDetailsEnricher.GetTraceId(ctx);

        Assert.Same(first, second);
    }

    // ── Enrich ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Enrich_SetsInstanceOnProblemDetails()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/1");
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails();

        ProblemDetailsEnricher.Enrich(pd, ctx);

        Assert.Equal("/api/orders/1", pd.Instance);
    }

    [Fact]
    public void Enrich_DoesNotOverwriteExistingInstance()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/1");
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Instance = "already-set"
        };

        ProblemDetailsEnricher.Enrich(pd, ctx);

        Assert.Equal("already-set", pd.Instance);
    }

    [Fact]
    public void Enrich_AddsRequestIdExtension()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/1");
        ctx.TraceIdentifier = "test-request-id";
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails();

        ProblemDetailsEnricher.Enrich(pd, ctx);

        Assert.True(pd.Extensions.ContainsKey("requestId"));
        Assert.Equal("test-request-id", pd.Extensions["requestId"]);
    }

    [Fact]
    public void Enrich_AddsTraceIdExtension()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/1");
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails();

        ProblemDetailsEnricher.Enrich(pd, ctx);

        Assert.True(pd.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public void Enrich_DoesNotOverwriteExistingRequestId()
    {
        var ctx = BuildHttpContext("api.example.com", "/api/orders/1");
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Extensions =
            {
                ["requestId"] = "original-id"
            }
        };

        ProblemDetailsEnricher.Enrich(pd, ctx);

        Assert.Equal("original-id", pd.Extensions["requestId"]);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DefaultHttpContext BuildHttpContext(string host,
        string path,
        string method = "GET")
    {
        var ctx = new DefaultHttpContext
        {
            Request =
            {
                Host = new HostString(host),
                Path = new PathString(path),
                Method = method
            }
        };
        return ctx;
    }
}
