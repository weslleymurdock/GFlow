namespace GFlow.Core.Workflows;

/// <summary>Represents a configured GitHub Action invocation.</summary>
public sealed class ActionStep : Step
{
    /// <summary>Creates an Action step.</summary>
    public ActionStep(string uses)
    {
        ArgumentException.ThrowIfNullOrEmpty(uses);
        Uses = uses;
    }

    /// <summary>The Action reference, such as <c>actions/checkout@v4</c>.</summary>
    public string Uses { get; set; }

    /// <summary>Arbitrary Action inputs represented by the generic YAML model.</summary>
    public GFlow.Core.Workflows.Yaml.YamlMapping With { get; } = new();
}

