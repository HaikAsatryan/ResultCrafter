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
    ///     Extension key carrying the <see cref="ResultCrafter.ErrorType" />
    ///     string. Your own <c>CustomizeProblemDetails</c> callback runs before ResultCrafter's and can branch on
    ///     it. Stripped before the response body is written to the client.
    /// </summary>
    public const string RcErrorId = "x-rc-error-id";

    /// <summary>
    ///     Extension key carrying the <c>Retry-After</c> value in whole seconds for an
    ///     <see cref="ResultCrafter.ErrorType.TooManyRequests" /> error built with a retry delay. The pipeline
    ///     moves it to the HTTP <c>Retry-After</c> header and strips it before the response body is written.
    /// </summary>
    public const string RcRetryAfter = "x-rc-retry-after";

    /// <summary>
    ///     Extension key for the structured field-errors dictionary on
    ///     <see cref="ResultCrafter.Error" /> instances that carry validation
    ///     errors. Serialised into the response body under the top-level <c>errors</c> key,
    ///     matching the shape produced by ASP.NET Core's built-in model validation responses.
    /// </summary>
    public const string Errors = "errors";

    /// <summary>
    ///     Extension key for the codes and args of the field errors, keyed like <see cref="Errors" />: entry <c>i</c>
    ///     of a field describes message <c>i</c> of the same field. Written only when a
    ///     <see cref="ResultCrafter.FieldError" /> carries a code or args.
    /// </summary>
    public const string ErrorCodes = "errorCodes";
}
