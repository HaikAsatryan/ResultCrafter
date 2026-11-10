using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace ResultCrafter.AspNetCore.ProblemDetails;

/// <summary>
///     Constructs <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" /> instances from
///     <see cref="Error" /> values, centralising status-code mapping, title resolution,
///     detail fallback, and internal marker extension population.
/// </summary>
/// <remarks>
///     The <c>x-rc</c>, <c>x-rc-error-id</c> and <c>x-rc-retry-after</c> extension keys are internal markers.
///     <c>AddResultCrafter()</c> strips them before the body is written to the client.
/// </remarks>
public static class ProblemDetailsBuilder
{
    /// <summary>
    ///     Builds a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails" /> from <paramref name="error" />.
    ///     The returned object is ready to be passed to <c>IProblemDetailsService.TryWriteAsync</c>
    ///     or converted to a <c>ProblemHttpResult</c> via <c>TypedResults.Problem</c>.
    /// </summary>
    /// <remarks>
    ///     Values added with <c>Error.With</c> are stored as <see cref="JsonElement" />. With reflection-based
    ///     JSON turned off (NativeAOT), ProblemDetails can write a <see cref="JsonElement" /> extension but not a
    ///     boxed <see cref="long" />, <see cref="bool" />, <see cref="Guid" /> and similar.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <see langword="null" />.</exception>
    public static Microsoft.AspNetCore.Mvc.ProblemDetails Build(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = HttpErrorCatalog.TypeUri(error.Type),
            Status = HttpErrorCatalog.Status(error.Type),
            Title = HttpErrorCatalog.Title(error.Type),
            Detail = HttpErrorCatalog.ResolveDetail(error),
            Extensions =
            {
                [ProblemDetailsKeys.RcMarker] = true,
                [ProblemDetailsKeys.RcErrorId] = error.Type.ToString()
            }
        };

        // Field-level errors are surfaced under the "errors" extension key,
        // matching the shape ASP.NET Core uses for model-validation responses.
        if (error.IsValidation) pd.Extensions[ProblemDetailsKeys.Errors] = CopyErrors(error.Errors);

        if (error.RetryAfter is { } wait) pd.Extensions[ProblemDetailsKeys.RcRetryAfter] = RetryAfterSeconds(wait);

        if (error.Extensions.Count > 0)
            foreach (var member in ToJson(error.Extensions).EnumerateObject())
                pd.Extensions[member.Name] = member.Value;

        return pd;
    }

    // The body gets its own arrays, so a CustomizeProblemDetails hook that rewrites messages (for example to
    // localize them) cannot change an Error the application cached and reuses.
    private static ReadOnlyDictionary<string, string[]> CopyErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        var copy = new Dictionary<string, string[]>(errors.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, messages) in errors) copy[key] = [.. messages];
        return new ReadOnlyDictionary<string, string[]>(copy);
    }

    // RFC 9110 Retry-After takes whole seconds. Rounding up never tells a client to retry too early.
    private static string RetryAfterSeconds(TimeSpan wait)
    {
        var seconds = wait.Ticks / TimeSpan.TicksPerSecond + (wait.Ticks % TimeSpan.TicksPerSecond == 0 ? 0 : 1);
        return seconds.ToString(CultureInfo.InvariantCulture);
    }

    private static JsonElement ToJson(IReadOnlyDictionary<string, object> extensions)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in extensions)
            {
                writer.WritePropertyName(key);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case Guid id:
                writer.WriteStringValue(id);
                break;
            case DateTimeOffset moment:
                writer.WriteStringValue(moment);
                break;
            case DateTime moment:
                writer.WriteStringValue(moment);
                break;
            case string[] items:
                writer.WriteStartArray();
                foreach (var item in items) writer.WriteStringValue(item);
                writer.WriteEndArray();
                break;
            default:
                // Error.With only accepts the types above.
                throw new InvalidOperationException($"Unsupported extension value type '{value.GetType().Name}'.");
        }
    }
}
