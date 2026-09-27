using GFlow.Core.Workflows.Yaml;

namespace GFlow.Core.Workflows;

/// <summary>Represents a GitHub Actions permissions mapping.</summary>
public sealed class PermissionSet
{
    private readonly Dictionary<string, PermissionLevel> _values = new(StringComparer.Ordinal);

    /// <summary>Defines how the permissions mapping is represented at the workflow level.</summary>
    public PermissionPolicy Policy { get; private set; } = PermissionPolicy.Explicit;

    /// <summary>Gets configured permission names and levels.</summary>
    public IReadOnlyDictionary<string, PermissionLevel> Values => _values;

    /// <summary>Sets the special <c>read-all</c> permissions policy.</summary>
    public void SetReadAll() => SetPolicy(PermissionPolicy.ReadAll);

    /// <summary>Sets the special <c>write-all</c> permissions policy.</summary>
    public void SetWriteAll() => SetPolicy(PermissionPolicy.WriteAll);

    /// <summary>Sets the explicit permissions mapping policy.</summary>
    public void SetExplicit() => SetPolicy(PermissionPolicy.Explicit);

    /// <summary>Sets a permission level.</summary>
    public void Set(string name, PermissionLevel level)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Policy = PermissionPolicy.Explicit;
        _values[name] = level;
    }

    private void SetPolicy(PermissionPolicy policy)
    {
        Policy = policy;
        _values.Clear();
    }

    /// <summary>Attempts to get a permission level.</summary>
    public bool TryGet(string name, out PermissionLevel level) => _values.TryGetValue(name, out level);

    /// <summary>Creates a generic YAML representation for serialization in a later stage.</summary>
    public YamlValue ToYaml()
    {
        if (Policy == PermissionPolicy.ReadAll)
            return new YamlScalar("read-all");
        if (Policy == PermissionPolicy.WriteAll)
            return new YamlScalar("write-all");

        var mapping = new YamlMapping();
        foreach (var pair in _values)
            mapping.Set(pair.Key, new YamlScalar(pair.Value switch
            {
                PermissionLevel.None => "none",
                PermissionLevel.Read => "read",
                PermissionLevel.Write => "write",
                _ => throw new ArgumentOutOfRangeException()
            }));
        return mapping;
    }
}

