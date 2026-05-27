namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///     String constants for ResultCrafter's internal and public ProblemDetails extension keys.
/// </summary>
public static class ProblemDetailsKeys
{
    /// <summary>
    ///     Extension key marking a ProblemDetails response as produced by ResultCrafter.
    ///     Present only during the pipeline; stripped before the response body is written
    ///     to the client by <c>ConfigureResultCrafterProblemDetails</c>.
    /// </summary>
    public const string RcMarker = "x-rc";

    /// <summary>
    ///     Extension key carrying the <see cref="ResultCrafter.Core.Primitives.ErrorType" />
    ///     string. Useful for custom middleware that needs to branch on error type.
    ///     Stripped before the response body is written to the client.
    /// </summary>
    public const string RcErrorId = "x-rc-error-id";

    /// <summary>
    ///     Extension key for the structured field-errors dictionary on
    ///     <see cref="ResultCrafter.Core.Primitives.Error" /> instances that carry validation
    ///     errors. Serialised into the response body under the top-level <c>errors</c> key,
    ///     matching the shape produced by ASP.NET Core's built-in model validation responses.
    /// </summary>
    public const string Errors = "errors";
}
