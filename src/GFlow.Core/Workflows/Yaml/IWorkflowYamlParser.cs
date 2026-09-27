namespace GFlow.Core.Workflows.Yaml;

/// <summary>Converts GitHub Actions YAML documents into workflow domain models.</summary>
public interface IWorkflowYamlParser
{
    /// <summary>Parses a complete GitHub Actions workflow YAML document.</summary>
    /// <param name="yaml">The YAML document to parse.</param>
    /// <returns>The parsed workflow.</returns>
    /// <exception cref="WorkflowYamlException">Thrown when the document is invalid or cannot be represented by the workflow model.</exception>
    Workflow Parse(string yaml);
}
