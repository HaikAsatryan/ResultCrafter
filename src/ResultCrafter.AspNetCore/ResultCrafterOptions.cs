using Microsoft.Extensions.Logging;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     Top-level configuration for ResultCrafter's exception and error-response behaviour.
///     Pass a configuration delegate to
///     <see cref="ServiceCollectionExtensions.AddResultCrafter" />.
/// </summary>
public sealed class ResultCrafterOptions
{
    /// <summary>
    ///     Controls how much exception detail is included in <c>500</c> ProblemDetails responses.
    ///     Defaults to <see cref="ExceptionDetailMode.Auto" />, which exposes details in
    ///     non-production environments and sanitises them in production.
    /// </summary>
    public ExceptionDetailMode ExceptionDetailMode { get; set; } = ExceptionDetailMode.Auto;

    /// <summary>
    ///     The <c>detail</c> message returned to clients in <c>500</c> responses when
    ///     <see cref="ExceptionDetailMode" /> is <see cref="ExceptionDetailMode.Sanitized" />
    ///     or <see cref="ExceptionDetailMode.Auto" /> in a production environment.
    ///     Defaults to <c>"an_unexpected_error_occurred"</c>.
    /// </summary>
    public string DefaultServerErrorMessage { get; set; } = "an_unexpected_error_occurred";

    /// <summary>
    ///     The <see cref="LogLevel" /> used when logging <c>4xx</c> client errors produced
    ///     by ResultCrafter. Defaults to <see cref="LogLevel.Warning" />.
    ///     Set to <see cref="LogLevel.Information" /> to reduce noise in high-traffic APIs,
    ///     or to <see cref="LogLevel.None" /> to suppress client-error logging entirely.
    /// </summary>
    public LogLevel ClientErrorLogLevel { get; set; } = LogLevel.Warning;
}

/// <summary>
///     Controls how much exception detail is exposed in <c>5xx</c> ProblemDetails responses.
/// </summary>
public enum ExceptionDetailMode
{
    /// <summary>
    ///     Includes the full exception string in non-production environments
    ///     (names containing "dev", "local", "test", "qa", "stage", "uat", "preprod",
    ///     "sandbox", or "debug"); sanitises the detail field in production.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     Never exposes exception details to the client regardless of environment.
    ///     The response <c>detail</c> field is set to
    ///     <see cref="ResultCrafterOptions.DefaultServerErrorMessage" />.
    /// </summary>
    Sanitized = 1,

    /// <summary>
    ///     Always exposes the full exception string.
    ///     Use only for internal or debug deployments — never in production.
    /// </summary>
    IncludeExceptionDetails = 2
}
