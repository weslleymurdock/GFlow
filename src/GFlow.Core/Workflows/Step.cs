using GFlow.Core.Workflows.Yaml;

namespace GFlow.Core.Workflows;

/// <summary>Base type for a GitHub Actions workflow step.</summary>
public abstract class Step
{
    /// <summary>Creates a step with a stable editor identity.</summary>
    protected Step()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>Stable editor/domain identity. It is not emitted to GitHub Actions YAML.</summary>
    public Guid Id { get; }

    /// <summary>Optional display name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional step condition.</summary>
    public string? If { get; set; }

    /// <summary>Whether failure of this step is allowed to continue the job.</summary>
    public bool? ContinueOnError { get; set; }

    /// <summary>Optional step timeout in minutes.</summary>
    public int? TimeoutMinutes { get; set; }

    /// <summary>Environment variables scoped to this step.</summary>
    public YamlMapping Environment { get; } = new();
}

