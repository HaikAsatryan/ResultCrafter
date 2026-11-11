using System.Text.Json.Serialization;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///     The shape of every ResultCrafter error body, for OpenAPI documents only. The <c>Produces*</c> builder
///     extensions and attributes declare it as the <c>application/problem+json</c> response schema, so API
///     clients see <c>traceId</c>, <c>requestId</c>, <c>errors</c> and <c>errorCodes</c>, which the plain
///     <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" /> schema does not list.
/// </summary>
/// <remarks>
///     ResultCrafter never creates or serializes this type; responses are written from
///     <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" />. Member names are fixed with
///     <see cref="JsonPropertyNameAttribute" /> because the response body uses these names whatever the app's
///     JSON naming policy is. Members added with <c>Error.With</c> are not listed.
/// </remarks>
public sealed class ProblemResponse
{
    /// <summary>A URI that identifies the problem type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>A short snake_case summary of the problem type, such as <c>not_found</c>.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>The HTTP status code.</summary>
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    /// <summary>A description of this occurrence of the problem.</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    /// <summary>The request path that produced the problem.</summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>The W3C trace ID of the request, for log correlation.</summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    /// <summary>The server's request identifier.</summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    /// <summary>Field-level error messages keyed by field name. Present only on validation errors.</summary>
    [JsonPropertyName("errors")]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>
    ///     The code and args of each message in <see cref="Errors" />, at the same key and index, so a client can show
    ///     the error in its own language. Present only when a field error carries a code or args.
    /// </summary>
    [JsonPropertyName("errorCodes")]
    public IReadOnlyDictionary<string, FieldErrorCode[]>? ErrorCodes { get; init; }
}
