using System.Text.Json;

namespace ResultCrafter.AspNetCore;

/// <summary>
///     Aligns field-error keys with the property names an app's JSON settings put on the wire, so a key such
///     as <c>Address.Street</c> becomes <c>address.street</c> under the camelCase policy.
/// </summary>
/// <remarks>
///     Used by ResultCrafter.AspNetCore.FluentValidation through InternalsVisibleTo: treat these members as public
///     API and keep them unchanged within a major version.
/// </remarks>
internal static class FieldKeyNaming
{
    /// <summary>
    ///     Re-keys <paramref name="errors" /> with <paramref name="policy" />. Keys that end up equal under
    ///     <paramref name="comparer" /> are merged, so no message is lost. A key that already follows the
    ///     policy stays the same.
    /// </summary>
    public static Dictionary<string, string[]> ConvertKeys(JsonNamingPolicy policy,
        IEnumerable<KeyValuePair<string, string[]>> errors,
        IEqualityComparer<string> comparer)
    {
        var converted = new Dictionary<string, string[]>(comparer);

        foreach (var (key, messages) in errors)
        {
            var name = ConvertPath(policy, key);
            converted[name] = converted.TryGetValue(name, out var existing) ? [.. existing, .. messages] : messages;
        }

        return converted;
    }

    /// <summary>
    ///     Applies <paramref name="policy" /> to each dot-separated segment of <paramref name="path" />, like
    ///     System.Text.Json names nested properties. An index suffix such as <c>[0]</c> is kept as is.
    /// </summary>
    public static string ConvertPath(JsonNamingPolicy policy, string path)
    {
        if (!path.Contains('.')) return ConvertSegment(policy, path);

        var segments = path.Split('.');
        for (var i = 0; i < segments.Length; i++) segments[i] = ConvertSegment(policy, segments[i]);

        return string.Join('.', segments);
    }

    private static string ConvertSegment(JsonNamingPolicy policy, string segment)
    {
        var index = segment.IndexOf('[');
        if (index < 0) return policy.ConvertName(segment);
        if (index == 0) return segment;

        // Only the name is converted; a policy such as snake_case could otherwise rewrite "[0]".
        return string.Concat(policy.ConvertName(segment[..index]), segment.AsSpan(index));
    }
}
