using GFlow.Core.Workflows.Yaml;

namespace GFlow.Core.Workflows;

/// <summary>Represents a framework-independent GitHub Actions workflow.</summary>
public sealed class Workflow
{
    private readonly List<Job> _jobs = [];

    /// <summary>Creates a workflow.</summary>
    public Workflow(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Id = Guid.NewGuid();
    }

    /// <summary>Stable editor/domain identity. It is not emitted to GitHub Actions YAML.</summary>
    public Guid Id { get; }

    /// <summary>The workflow display name.</summary>
    public string Name { get; set; }

    /// <summary>Workflow trigger events.</summary>
    public TriggerCollection Triggers { get; } = new();

    /// <summary>Workflow-scoped environment variables.</summary>
    public YamlMapping Environment { get; } = new();

    /// <summary>Workflow-level permissions.</summary>
    public PermissionSet Permissions { get; } = new();

    /// <summary>Workflow jobs.</summary>
    public IReadOnlyList<Job> Jobs => _jobs;

    /// <summary>Adds a job and prevents duplicate GitHub job identifiers.</summary>
    public void AddJob(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (_jobs.Any(existing => StringComparer.Ordinal.Equals(existing.Id, job.Id)))
            throw new ArgumentException($"A job with identifier '{job.Id}' already exists.", nameof(job));
        _jobs.Add(job);
    }
}

