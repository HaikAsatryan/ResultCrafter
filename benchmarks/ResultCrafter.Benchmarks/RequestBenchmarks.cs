using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Benchmarks;

/// <summary>
///     Suite B, the whole request: <c>GET /orders/{id}</c> through TestServer, one app per variant. Every variant
///     except the floor runs the same ResultCrafter app and writes its 404 through the same
///     <see cref="IProblemDetailsService" />.
/// </summary>
[MemoryDiagnoser]
public class RequestBenchmarks
{
    // endpoint -> service -> repository
    private const int Depth = 3;
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";

    private readonly List<WebApplication> _apps = [];
    private HttpClient _exceptionHandler = null!;
    private HttpClient _tryCatchMiddleware = null!;
    private HttpClient _resultCrafter = null!;
    private HttpClient _union = null!;
    private HttpClient _floor = null!;
    private HttpClient _logFiltered = null!;
    private HttpClient _logEnabled = null!;
    private string _path = null!;

    [Params(200, 404)]
    public int Status { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _path = $"/orders/{(Status == 200 ? Orders.KnownId : Orders.MissingId)}";

        _exceptionHandler = await StartAsync(
            b => b.Services.AddExceptionHandler<OrderNotFoundHandler>().AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                MapThrowing(a);
            });

        _tryCatchMiddleware = await StartAsync(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                var problems = a.Services.GetRequiredService<IProblemDetailsService>();
                a.Use(async (context, next) =>
                {
                    try
                    {
                        await next(context);
                    }
                    catch (OrderNotFoundException)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await problems.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context,
                            ProblemDetails = NotFoundProblem()
                        });
                    }
                });
                MapThrowing(a);
            });

        _resultCrafter = await StartAsync(b => b.Services.AddResultCrafter(), MapResultCrafter);

        _union = await StartAsync(
            b => b.Services.AddResultCrafter(),
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/orders/{id:int}", async Task<Results<Ok<Order>, ProblemHttpResult>> (int id) =>
                    await Orders.GetUnionAsync(id, Depth) switch
                    {
                        Order order => TypedResults.Ok(order),
                        OrderNotFound notFound => TypedResults.Problem(notFound.Detail,
                            statusCode: StatusCodes.Status404NotFound,
                            title: "not_found",
                            type: NotFoundType)
                    });
            });

        _floor = await StartAsync(
            _ => { },
            a => a.MapGet("/orders/{id:int}", async Task<Results<Ok<Order>, NotFound>> (int id) =>
                await Orders.GetOrNullAsync(id, Depth) is { } order ? TypedResults.Ok(order) : TypedResults.NotFound()));

        // Same sink in both, so the only difference is whether the 4xx Warning entry passes the level filter.
        _logFiltered = await StartAsync(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Logging.AddProvider(new FormattingSink()).SetMinimumLevel(LogLevel.Error);
            },
            MapResultCrafter);

        _logEnabled = await StartAsync(
            b =>
            {
                b.Services.AddResultCrafter();
                b.Logging.AddProvider(new FormattingSink()).SetMinimumLevel(LogLevel.Warning);
            },
            MapResultCrafter);

        await VerifySameResponsesAsync();
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        foreach (var app in _apps) await app.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public Task<int> ExceptionHandler()
    {
        return SendAsync(_exceptionHandler);
    }

    [Benchmark]
    public Task<int> TryCatchMiddleware()
    {
        return SendAsync(_tryCatchMiddleware);
    }

    [Benchmark]
    public Task<int> ResultCrafter()
    {
        return SendAsync(_resultCrafter);
    }

    [Benchmark]
    public Task<int> Union()
    {
        return SendAsync(_union);
    }

    [Benchmark]
    public Task<int> Floor()
    {
        return SendAsync(_floor);
    }

    [Benchmark]
    public Task<int> ResultCrafterLogFiltered()
    {
        return SendAsync(_logFiltered);
    }

    [Benchmark]
    public Task<int> ResultCrafterLogEnabled()
    {
        return SendAsync(_logEnabled);
    }

    private async Task<int> SendAsync(HttpClient client)
    {
        using var response = await client.GetAsync(_path);
        return (int)response.StatusCode;
    }

    private async Task<HttpClient> StartAsync(Action<WebApplicationBuilder> configure, Action<WebApplication> pipeline)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        configure(builder);

        var app = builder.Build();
        pipeline(app);
        await app.StartAsync();
        _apps.Add(app);
        return app.GetTestClient();
    }

    private static void MapThrowing(WebApplication app)
    {
        app.MapGet("/orders/{id:int}", async (int id) => TypedResults.Ok(await Orders.GetOrThrowAsync(id, Depth)));
    }

    private static void MapResultCrafter(WebApplication app)
    {
        app.UseResultCrafter();
        app.MapGet("/orders/{id:int}", async (int id) => (await Orders.GetResultAsync(id, Depth)).ToOkResult());
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails NotFoundProblem()
    {
        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = NotFoundType,
            Title = "not_found",
            Status = StatusCodes.Status404NotFound,
            Detail = Orders.Detail
        };
    }

    // Fairness gate: every variant must send the same status, content type and body as ResultCrafter (traceId and
    // requestId differ per request and are ignored), so nothing unequal is measured. The floor only has to match
    // the status, and sends no 404 body by design.
    private async Task VerifySameResponsesAsync()
    {
        var expected = await ProbeAsync(_resultCrafter);
        if (expected.Status != Status)
            throw new InvalidOperationException($"ResultCrafter answered {expected.Status}, expected {Status}.");
        if (Status == 404 && expected.Body?["detail"]?.GetValue<string>() != Orders.Detail)
            throw new InvalidOperationException($"Unexpected 404 body: {expected.Body}");

        var variants = new Dictionary<string, HttpClient>
        {
            [nameof(ExceptionHandler)] = _exceptionHandler,
            [nameof(TryCatchMiddleware)] = _tryCatchMiddleware,
            [nameof(Union)] = _union,
            [nameof(Floor)] = _floor,
            [nameof(ResultCrafterLogFiltered)] = _logFiltered,
            [nameof(ResultCrafterLogEnabled)] = _logEnabled
        };

        foreach (var (name, client) in variants)
        {
            var actual = await ProbeAsync(client);
            var same = name == nameof(Floor) && Status == 404
                ? actual.Status == 404 && actual.Body is null
                : actual.Status == expected.Status &&
                  actual.ContentType == expected.ContentType &&
                  JsonNode.DeepEquals(actual.Body, expected.Body);

            if (!same)
                throw new InvalidOperationException(
                    $"{name} differs from ResultCrafter: {actual.Status} {actual.ContentType} {actual.Body} vs " +
                    $"{expected.Status} {expected.ContentType} {expected.Body}");
        }
    }

    private async Task<(int Status, string? ContentType, JsonNode? Body)> ProbeAsync(HttpClient client)
    {
        using var response = await client.GetAsync(_path);
        var text = await response.Content.ReadAsStringAsync();
        var body = text.Length == 0 ? null : JsonNode.Parse(text);
        if (body is JsonObject json)
        {
            json.Remove("traceId");
            json.Remove("requestId");
        }

        return ((int)response.StatusCode, response.Content.Headers.ContentType?.ToString(), body);
    }

    private sealed class OrderNotFoundHandler(IProblemDetailsService problems) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not OrderNotFoundException) return false;

            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = NotFoundProblem(),
                Exception = exception
            });
        }
    }

    // Formats every entry it receives and drops it: the least a real logging provider does.
    private sealed class FormattingSink : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = formatter(state, exception);
        }

        public void Dispose()
        {
        }
    }
}
