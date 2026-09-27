using GFlow.Core.Workflows.Yaml;

namespace GFlow.Core.Workflows;

/// <summary>Represents a GitHub Actions job matrix strategy.</summary>
public sealed class MatrixStrategy
{
    /// <summary>Whether a matrix run is cancelled when one job fails.</summary>
    public bool? FailFast { get; set; }

    /// <summary>The maximum number of matrix jobs that may run concurrently.</summary>
    public int? MaxParallel { get; set; }

    /// <summary>Named matrix dimensions and their arbitrary YAML values.</summary>
    public YamlMapping Dimensions { get; } = new();

    /// <summary>Additional matrix combinations.</summary>
    public YamlSequence Include { get; } = new();

    /// <summary>Matrix combinations to remove.</summary>
    public YamlSequence Exclude { get; } = new();
}

