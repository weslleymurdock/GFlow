using GFlow.Core.Workflows.Yaml;

namespace GFlow.Core.Workflows;

/// <summary>Represents a GitHub Actions job.</summary>
public sealed class Job
{
    private readonly List<string> _needs = [];
    private readonly List<Step> _steps = [];

    /// <summary>Creates a job with a GitHub job identifier.</summary>
    public Job(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        EditorId = Guid.NewGuid();
    }

    /// <summary>The GitHub job identifier.</summary>
    public string Id { get; }

    /// <summary>Stable editor identity. It is not emitted as a separate YAML property.</summary>
    public Guid EditorId { get; }

    /// <summary>Optional display name.</summary>
    public string? Name { get; set; }

    /// <summary>The runner expression or label represented as a YAML value.</summary>
    public YamlValue? RunsOn { get; set; }

    /// <summary>Job identifiers this job depends on.</summary>
    public IReadOnlyList<string> Needs => _needs;

    /// <summary>Steps executed by the job.</summary>
    public IReadOnlyList<Step> Steps => _steps;

    /// <summary>Job-scoped environment variables.</summary>
    public YamlMapping Environment { get; } = new();

    /// <summary>Job permissions.</summary>
    public PermissionSet Permissions { get; } = new();

    /// <summary>Optional matrix execution strategy.</summary>
    public MatrixStrategy? Strategy { get; set; }

    /// <summary>Optional job condition.</summary>
    public string? If { get; set; }

    /// <summary>Adds a dependency on another job.</summary>
    /// <exception cref="ArgumentException">Thrown for an empty identifier or self-dependency.</exception>
    public void AddNeed(string jobId)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobId);
        if (StringComparer.Ordinal.Equals(jobId, Id))
            throw new ArgumentException("A job cannot depend on itself.", nameof(jobId));
        if (!_needs.Contains(jobId, StringComparer.Ordinal))
            _needs.Add(jobId);
    }

    /// <summary>Adds a workflow step.</summary>
    public void AddStep(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);
    }
}

