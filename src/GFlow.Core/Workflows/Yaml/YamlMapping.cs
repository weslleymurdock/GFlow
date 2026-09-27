namespace GFlow.Core.Workflows.Yaml;

/// <summary>Represents a YAML mapping whose values can be arbitrarily nested.</summary>
public sealed class YamlMapping : YamlValue, IReadOnlyDictionary<string, YamlValue>
{
    private readonly Dictionary<string, YamlValue> _values = new(StringComparer.Ordinal);

    /// <summary>Creates an empty mapping.</summary>
    public YamlMapping() { }

    /// <summary>Creates a mapping from key/value pairs.</summary>
    public YamlMapping(IEnumerable<KeyValuePair<string, YamlValue>> values)
    {
        foreach (var pair in values)
            Set(pair.Key, pair.Value);
    }

    /// <summary>The number of entries in the mapping.</summary>
    public int Count => _values.Count;

    /// <summary>Gets a value by key.</summary>
    public YamlValue this[string key] => _values[key];

    /// <summary>Gets the mapping keys.</summary>
    public IEnumerable<string> Keys => _values.Keys;

    /// <summary>Gets the mapping values.</summary>
    public IEnumerable<YamlValue> Values => _values.Values;

    /// <summary>Adds or replaces a mapping value.</summary>
    public void Set(string key, YamlValue value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        _values[key] = value;
    }

    /// <summary>Attempts to get a mapping value.</summary>
    public bool TryGetValue(string key, out YamlValue value) => _values.TryGetValue(key, out value!);

    /// <summary>Returns an enumerator for the mapping.</summary>
    public IEnumerator<KeyValuePair<string, YamlValue>> GetEnumerator() => _values.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a mapping from object values using supported YAML scalar types.</summary>
    public static YamlMapping FromObjectValues(IEnumerable<KeyValuePair<string, object?>> values)
    {
        var mapping = new YamlMapping();
        foreach (var pair in values)
            mapping.Set(pair.Key, FromObject(pair.Value));
        return mapping;
    }

    private static YamlValue FromObject(object? value) => value switch
    {
        null => YamlScalar.Null,
        string text => new YamlScalar(text),
        bool boolean => new YamlScalar(boolean),
        int integer => new YamlScalar(integer),
        long integer => new YamlScalar(integer),
        decimal number => new YamlScalar(number),
        _ => throw new ArgumentException($"Unsupported YAML scalar type: {value.GetType().FullName}.", nameof(value))
    };
}

