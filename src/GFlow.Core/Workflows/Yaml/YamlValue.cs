namespace GFlow.Core.Workflows.Yaml;

/// <summary>Represents an arbitrary YAML value.</summary>
public abstract class YamlValue
{
    /// <summary>Creates a scalar YAML value from a supported CLR scalar.</summary>
    public static YamlScalar Scalar(string value) => new(value);

    /// <summary>Creates a scalar YAML value from a Boolean.</summary>
    public static YamlScalar Scalar(bool value) => new(value);

    /// <summary>Creates a scalar YAML value from an integer.</summary>
    public static YamlScalar Scalar(long value) => new(value);

    /// <summary>Creates a scalar YAML value from a number.</summary>
    public static YamlScalar Scalar(decimal value) => new(value);

    /// <summary>Creates a null YAML scalar value.</summary>
    public static YamlScalar Null() => YamlScalar.Null;

    /// <summary>Creates an empty YAML mapping.</summary>
    public static YamlMapping Mapping() => new();

    /// <summary>Creates a YAML mapping from key/value pairs.</summary>
    public static YamlMapping Mapping(IEnumerable<KeyValuePair<string, YamlValue>> values) => new(values);

    /// <summary>Creates an empty YAML sequence.</summary>
    public static YamlSequence Sequence() => new();

    /// <summary>Creates a YAML sequence from values.</summary>
    public static YamlSequence Sequence(IEnumerable<YamlValue> values) => new(values);
}

