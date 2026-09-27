namespace GFlow.Core.Workflows.Yaml;

/// <summary>Represents an error while parsing or serializing a workflow YAML document.</summary>
public sealed class WorkflowYamlException : Exception
{
    /// <summary>Creates a workflow YAML exception.</summary>
    public WorkflowYamlException(string message) : base(message) { }

    /// <summary>Creates a workflow YAML exception with an underlying cause.</summary>
    public WorkflowYamlException(string message, Exception innerException) : base(message, innerException) { }
}
