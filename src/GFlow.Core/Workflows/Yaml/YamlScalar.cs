namespace GFlow.Core.Workflows.Yaml;

/// <summary>Identifies the scalar representation of a YAML value.</summary>
public enum YamlScalarKind
{
    String,
    Boolean,
    Integer,
    Number,
    Null
}

/// <summary>Represents a YAML scalar without reducing typed values to strings.</summary>
public sealed class YamlScalar : YamlValue
{
    /// <summary>The canonical null scalar.</summary>
    public static YamlScalar Null { get; } = new(YamlScalarKind.Null, null);

    /// <summary>Creates a string scalar.</summary>
    public YamlScalar(string value) : this(YamlScalarKind.String, value ?? throw new ArgumentNullException(nameof(value))) { }

    /// <summary>Creates a Boolean scalar.</summary>
    public YamlScalar(bool value) : this(YamlScalarKind.Boolean, value) { }

    /// <summary>Creates an integer scalar.</summary>
    public YamlScalar(long value) : this(YamlScalarKind.Integer, value) { }

    /// <summary>Creates a numeric scalar.</summary>
    public YamlScalar(decimal value) : this(YamlScalarKind.Number, value) { }

    private YamlScalar(YamlScalarKind kind, object? value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>The scalar kind.</summary>
    public YamlScalarKind Kind { get; }

    /// <summary>The typed scalar value, or <see langword="null"/> for a null scalar.</summary>
    public object? Value { get; }

    /// <summary>Gets the scalar value as a typed value.</summary>
    public T GetValue<T>() => Value is T typed
        ? typed
        : throw new InvalidCastException($"The YAML scalar contains {Value?.GetType().Name ?? "null"}, not {typeof(T).Name}.");
}

