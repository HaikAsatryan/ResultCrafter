using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     Refuses to serialize <see cref="Result{T}" />, <see cref="Result" /> and <see cref="Error" /> as an HTTP
///     response body. An endpoint that returns one of them without mapping it would otherwise send the
///     result as JSON with status 200, even on failure.
/// </summary>
/// <remarks>
///     Registered only in the app's HTTP JSON options (Minimal API and MVC), never as an attribute on the
///     types, so code that serializes these types with its own options (a cache, a message bus) keeps working.
///     <see cref="CreateConverter" /> throws instead of building a converter, so no generic type is ever
///     constructed at runtime and the factory stays NativeAOT-safe.
/// </remarks>
internal sealed class UnmappedResultJsonConverterFactory : JsonConverterFactory
{
    public static readonly UnmappedResultJsonConverterFactory Instance = new();

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(Error) ||
               typeToConvert == typeof(Result) ||
               (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>));
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var name = typeToConvert.IsGenericType
            ? $"Result<{typeToConvert.GetGenericArguments()[0].Name}>"
            : typeToConvert.Name;

        throw new InvalidOperationException(
            $"ResultCrafter: {name} cannot be written as an HTTP response body. Sent as JSON it would go out " +
            "with status 200 even when it holds an error. Map it in the endpoint instead: ToOkResult(), " +
            "ToCreatedResult(), ToAcceptedResult() or ToNoContentResult() for a result, ToProblemResult() for " +
            "an Error.");
    }
}
