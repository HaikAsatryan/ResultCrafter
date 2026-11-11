using System.Collections.ObjectModel;

namespace ResultCrafter;

// Error.Extensions and FieldError.Args hold only the value types the ASP.NET Core integration can write as JSON
// without reflection, so both share one copy, equality and hash rule.
internal static class TypedValues
{
    public static readonly IReadOnlyDictionary<string, object> Empty = ReadOnlyDictionary<string, object>.Empty;

    public static IReadOnlyDictionary<string, object> With(IReadOnlyDictionary<string, object> source,
        string key,
        object value)
    {
        var copy = new Dictionary<string, object>(source.Count + 1, StringComparer.Ordinal);
        foreach (var (existingKey, existingValue) in source) copy[existingKey] = existingValue;
        copy[key] = value;

        return new ReadOnlyDictionary<string, object>(copy);
    }

    public static bool Equal(IReadOnlyDictionary<string, object> a, IReadOnlyDictionary<string, object> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;

        foreach (var (key, aValue) in a)
        {
            if (!b.TryGetValue(key, out var bValue)) return false;

            var equal = aValue is string[] aArray
                ? bValue is string[] bArray && ArrayEquals(aArray, bArray)
                : aValue.Equals(bValue);

            if (!equal) return false;
        }

        return true;
    }

    // Summed so the result does not depend on dictionary order.
    public static int Hash(IReadOnlyDictionary<string, object> values)
    {
        var entries = 0;
        foreach (var (key, value) in values)
        {
            entries += HashCode.Combine(StringComparer.Ordinal.GetHashCode(key),
                value is string[] array ? ArrayHash(array) : value.GetHashCode());
        }

        return HashCode.Combine(values.Count, entries);
    }

    public static bool ArrayEquals(string[] a, string[] b)
    {
        return a.AsSpan().SequenceEqual(b);
    }

    public static int ArrayHash(string[] values)
    {
        var hash = new HashCode();
        hash.Add(values.Length);
        foreach (var value in values) hash.Add(value, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
