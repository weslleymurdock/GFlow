namespace GFlow.Core.Workflows.Yaml;

/// <summary>Represents an ordered YAML sequence whose items can be arbitrarily nested.</summary>
public sealed class YamlSequence : YamlValue, IReadOnlyList<YamlValue>
{
    private readonly List<YamlValue> _items = [];

    /// <summary>Creates an empty sequence.</summary>
    public YamlSequence() { }

    /// <summary>Creates a sequence from values.</summary>
    public YamlSequence(IEnumerable<YamlValue> values)
    {
        foreach (var value in values)
            Add(value);
    }

    /// <summary>The number of sequence items.</summary>
    public int Count => _items.Count;

    /// <summary>Gets a sequence item by index.</summary>
    public YamlValue this[int index] => _items[index];

    /// <summary>Appends a value to the sequence.</summary>
    public void Add(YamlValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _items.Add(value);
    }

    /// <summary>Returns an enumerator for the sequence.</summary>
    public IEnumerator<YamlValue> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

