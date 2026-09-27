namespace GFlow.Core.Workflows.Yaml;

/// <summary>Converts workflow domain models into GitHub Actions YAML documents.</summary>
public interface IWorkflowYamlSerializer
{
    /// <summary>Serializes a complete workflow into valid GitHub Actions YAML.</summary>
    /// <param name="workflow">The workflow to serialize.</param>
    /// <returns>The serialized YAML document.</returns>
    /// <exception cref="WorkflowYamlException">Thrown when the workflow contains unsupported or invalid data.</exception>
    string Serialize(Workflow workflow);
}
