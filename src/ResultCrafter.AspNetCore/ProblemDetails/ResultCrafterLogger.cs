using Microsoft.Extensions.Logging;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>Source-generated structured log methods used by the ResultCrafter pipeline.</summary>
internal static partial class ResultCrafterLogger
{
    /// <summary>
    ///     Logs a <c>4xx</c> client error at the level specified by
    ///     <see cref="ResultCrafterOptions.ClientErrorLogLevel" />.
    ///     The <paramref name="level" /> parameter is required because the log level is
    ///     configurable at runtime; omitting the <c>Level</c> attribute on
    ///     <c>[LoggerMessage]</c> delegates level selection to the call site.
    /// </summary>
    [LoggerMessage(
        EventId = 1,
        Message = "Client error {StatusCode}: {Title}, {Detail} (errorId: {ErrorId}) " +
                  "{HttpMethod} {Path} {Instance} (traceId: {TraceId}, requestId: {RequestId})")]
    public static partial void LogClientError(ILogger logger,
        LogLevel level,
        int statusCode,
        string? title,
        string? detail,
        string errorId,
        string httpMethod,
        string path,
        string instance,
        string traceId,
        string requestId);

    /// <summary>
    ///     Logs an unhandled exception that is being converted to a <c>500</c> ProblemDetails
    ///     response by <see cref="ResultCrafterExceptionHandler" />.
    ///     Always logged at <see cref="LogLevel.Error" />.
    /// </summary>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Unhandled exception {StatusCode} {HttpMethod} {Path} {Instance} " +
                  "(traceId: {TraceId}, requestId: {RequestId})")]
    public static partial void LogUnhandledException(ILogger logger,
        Exception exception,
        int statusCode,
        string httpMethod,
        string path,
        string instance,
        string traceId,
        string requestId);

    /// <summary>
    ///     Logged when the response stream has already started and ResultCrafter cannot
    ///     write a ProblemDetails body for an unhandled exception.
    ///     Logged at <see cref="LogLevel.Warning" />.
    /// </summary>
    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Response already started for {HttpMethod} {Path}; " +
                  "ResultCrafter cannot write ProblemDetails for the unhandled exception.")]
    public static partial void LogResponseAlreadyStarted(ILogger logger,
        string httpMethod,
        string path);
}
