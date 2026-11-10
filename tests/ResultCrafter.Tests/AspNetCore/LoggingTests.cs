using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using ResultCrafter.AspNetCore;

namespace ResultCrafter.Tests.AspNetCore;

/// <summary>
///     <see cref="TestApp" /> clears the logging providers, so no other test runs a log call. These tests
///     capture the entries to prove the 4xx and 500 log lines are written, at the right level, with the
///     request in them.
/// </summary>
public sealed class LoggingTests
{
    private static readonly string ClientErrorCategory = typeof(ConfigureResultCrafterProblemDetails).FullName!;
    private static readonly string ServerErrorCategory = typeof(ResultCrafterExceptionHandler).FullName!;

    [Fact]
    public async Task ResultClientError_IsLoggedAtWarningByDefault()
    {
        var logs = new CapturingLoggerProvider();

        var status = await Send(logs, "/missing");

        Assert.Equal(HttpStatusCode.NotFound, status);
        var entry = Assert.Single(logs.Entries, e => e.Category == ClientErrorCategory);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(1, entry.EventId);
        Assert.Contains("Client error 404", entry.Message, StringComparison.Ordinal);
        Assert.Contains("item_missing", entry.Message, StringComparison.Ordinal);
        Assert.Contains("(errorId: NotFound)", entry.Message, StringComparison.Ordinal);
        Assert.Contains("GET /missing", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultClientError_UsesConfiguredLevel()
    {
        var logs = new CapturingLoggerProvider();

        await Send(logs, "/missing", o => o.ClientErrorLogLevel = LogLevel.Information);

        Assert.Equal(LogLevel.Information, Assert.Single(logs.Entries, e => e.Category == ClientErrorCategory).Level);
    }

    [Fact]
    public async Task ClientErrorNotFromResultCrafter_IsNotLogged()
    {
        var logs = new CapturingLoggerProvider();

        var status = await Send(logs, "/framework-problem");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.DoesNotContain(logs.Entries, e => e.Category == ClientErrorCategory);
    }

    [Fact]
    public async Task UnhandledException_IsLoggedAsErrorWithTheException()
    {
        var logs = new CapturingLoggerProvider();

        var status = await Send(logs, "/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        var entry = Assert.Single(logs.Entries, e => e.Category == ServerErrorCategory);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(2, entry.EventId);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains("Unhandled exception 500 GET /boom", entry.Message, StringComparison.Ordinal);
        // The 500 body carries the same internal marker as a Result error; it must not also log as a client error.
        Assert.DoesNotContain(logs.Entries, e => e.Category == ClientErrorCategory);
    }

    // 400 is the lower edge of the client-error range.
    [Fact]
    public async Task ResultBadRequest_IsLoggedAsClientError()
    {
        var logs = new CapturingLoggerProvider();

        var status = await Send(logs, "/bad");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        var entry = Assert.Single(logs.Entries, e => e.Category == ClientErrorCategory);
        Assert.Contains("Client error 400", entry.Message, StringComparison.Ordinal);
    }

    private static async Task<HttpStatusCode> Send(CapturingLoggerProvider logs,
        string path,
        Action<ResultCrafterOptions>? configure = null)
    {
        await using var app = TestApp.Build(
            b =>
            {
                b.Logging.AddProvider(logs);
                b.Services.AddResultCrafter(configure);
            },
            a =>
            {
                a.UseResultCrafter();
                a.MapGet("/missing", () => Error.NotFound("item_missing").ToProblemResult());
                a.MapGet("/bad", () => Error.BadRequest("name_required").ToProblemResult());
                a.MapGet("/framework-problem", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));
                a.MapGet("/boom", IResult () => throw new InvalidOperationException("boom"));
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}

internal sealed record LogEntry(string Category, LogLevel Level, int EventId, string Message, Exception? Exception);

/// <summary>Keeps every log entry in memory so a test can assert on it.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(categoryName, _entries);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
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
            entries.Enqueue(new LogEntry(category, logLevel, eventId.Id, formatter(state, exception), exception));
        }
    }
}
