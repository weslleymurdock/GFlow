using GFlow.Core.Workflows;
using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;

namespace GFlow.Application.RepositoryWorkflow;

/// <summary>Provides repository and workflow management for the authenticated GitHub account.</summary>
public interface IRepositoryWorkflowService
{
    /// <summary>Gets the current repository/workflow context.</summary>
    RepositoryWorkflowContext Context { get; }

    /// <summary>Ensures that a valid GitHub authentication session is available.</summary>
    Task<GitHubAuthenticationResult> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists repositories accessible to the authenticated account.</summary>
    Task<IReadOnlyList<GitHubRepositoryInfo>> ListRepositoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a repository by owner and name and selects it.</summary>
    Task<GitHubRepositoryInfo> SelectRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default);

    /// <summary>Creates and selects a repository for the authenticated account.</summary>
    Task<GitHubRepositoryInfo> CreateRepositoryAsync(GitHubRepositoryCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists branches for the selected repository.</summary>
    Task<IReadOnlyList<GitHubBranchInfo>> ListBranchesAsync(CancellationToken cancellationToken = default);

    /// <summary>Selects a branch and preserves it as workflow context.</summary>
    Task<GitHubBranchInfo> SelectBranchAsync(string branchName, CancellationToken cancellationToken = default);

    /// <summary>Selects the selected repository's default branch.</summary>
    Task<GitHubBranchInfo> SelectDefaultBranchAsync(CancellationToken cancellationToken = default);

    /// <summary>Discovers YAML workflow files in the selected repository and branch.</summary>
    Task<IReadOnlyList<GitHubFileInfo>> DiscoverWorkflowsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads and parses an existing workflow while preserving its blob SHA.</summary>
    Task<WorkflowDocumentState> LoadWorkflowAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Creates a new workflow editing state using a safe .yml filename.</summary>
    WorkflowDocumentState CreateWorkflow(string name);

    /// <summary>Saves the current workflow using create semantics for new files and SHA-aware update semantics for existing files.</summary>
    Task<WorkflowDocumentState> SaveWorkflowAsync(WorkflowDocumentState document, CancellationToken cancellationToken = default);
}
