using GFlow.Core.Workflows;
using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;

namespace GFlow.Application.RepositoryWorkflow;

/// <summary>Holds the non-sensitive GitHub repository and workflow editing context.</summary>
public sealed class RepositoryWorkflowContext
{
    /// <summary>Gets the currently authenticated account.</summary>
    public GitHubAccount? Account { get; internal set; }

    /// <summary>Gets the selected repository.</summary>
    public GitHubRepositoryInfo? Repository { get; internal set; }

    /// <summary>Gets the selected branch.</summary>
    public GitHubBranchInfo? Branch { get; internal set; }

    /// <summary>Gets the selected workflow editing state.</summary>
    public WorkflowDocumentState? Workflow { get; internal set; }

    /// <summary>Clears repository, branch, and workflow selections without touching credentials.</summary>
    public void ClearRepositorySelection()
    {
        Repository = null;
        Branch = null;
        Workflow = null;
    }
}

/// <summary>Represents a workflow loaded or created for GitHub persistence.</summary>
public sealed record WorkflowDocumentState(
    Workflow Model,
    string Path,
    string Branch,
    string? BlobSha,
    bool IsNew);
