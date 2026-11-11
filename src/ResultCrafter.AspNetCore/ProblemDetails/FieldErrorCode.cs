using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///     One entry of the <c>errorCodes</c> member of a ResultCrafter error body, for OpenAPI documents only. Entry
///     <c>i</c> of a field describes message <c>i</c> of the same field in <c>errors</c>.
/// </summary>
/// <remarks>
///     ResultCrafter never creates or serializes this type; see <see cref="ProblemResponse" />. The body comes from
///     a <see cref="ResultCrafter.FieldError" />.
/// </remarks>
public sealed class FieldErrorCode
{
    /// <summary>
    ///     A stable code for the failed rule, such as <c>email_in_use</c> or FluentValidation's
    ///     <c>NotEmptyValidator</c>, or <see langword="null" /> when the rule has none.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>The values the message needs, such as <c>maxLength</c>. Absent when there are none.</summary>
    [JsonPropertyName("args")]
    public IReadOnlyDictionary<string, JsonElement>? Args { get; init; }
}
