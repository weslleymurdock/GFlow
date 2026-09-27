using System.Text.RegularExpressions;
using GFlow.Core.Workflows;
using GFlow.Core.Workflows.Yaml;
using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;

namespace GFlow.Application.RepositoryWorkflow;

/// <inheritdoc />
public sealed partial class RepositoryWorkflowService(
    IGitHubAuthenticationService authentication,
    IGitHubRepositoryService repositories,
    IGitHubBranchService branches,
    IGitHubFileService files,
    IGitHubWorkflowService workflows,
    IWorkflowYamlParser parser,
    IWorkflowYamlSerializer serializer) : IRepositoryWorkflowService
{
    /// <inheritdoc />
    public RepositoryWorkflowContext Context { get; } = new();

    /// <inheritdoc />
    public async Task<GitHubAuthenticationResult> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var state = authentication.GetCurrentState();
        if (state.State == GitHubAuthenticationState.Authenticated)
        {
            Context.Account = state.Account;
            return state;
        }

        state = await authentication.ValidateAsync(cancellationToken).ConfigureAwait(false);
        if (state.State == GitHubAuthenticationState.Authenticated)
            Context.Account = state.Account;

        return state;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubRepositoryInfo>> ListRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        return await repositories.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GitHubRepositoryInfo> SelectRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var selected = await repositories.GetAsync(owner, repository, cancellationToken).ConfigureAwait(false);
        Context.Repository = selected;
        Context.Branch = null;
        Context.Workflow = null;
        return selected;
    }

    /// <inheritdoc />
    public async Task<GitHubRepositoryInfo> CreateRepositoryAsync(GitHubRepositoryCreateRequest request, CancellationToken cancellationToken = default)
    {
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var created = await repositories.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        Context.Repository = created;
        Context.Branch = null;
        Context.Workflow = null;
        return created;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubBranchInfo>> ListBranchesAsync(CancellationToken cancellationToken = default)
    {
        var repository = RequireRepository();
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        return await branches.ListAsync(Owner(repository), RepositoryName(repository), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GitHubBranchInfo> SelectBranchAsync(string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        var repository = RequireRepository();
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var available = await branches.ListAsync(Owner(repository), RepositoryName(repository), cancellationToken: cancellationToken).ConfigureAwait(false);
        var selected = available.FirstOrDefault(branch => string.Equals(branch.Name, branchName, StringComparison.Ordinal));
        if (selected is null)
            throw new GitHubServiceException(GitHubErrorCategory.NotFound, $"Branch '{branchName}' was not found.");
        Context.Branch = selected;
        Context.Workflow = null;
        return selected;
    }

    /// <inheritdoc />
    public async Task<GitHubBranchInfo> SelectDefaultBranchAsync(CancellationToken cancellationToken = default)
    {
        var repository = RequireRepository();
        var defaultName = repository.DefaultBranch;
        if (string.IsNullOrWhiteSpace(defaultName))
            throw new GitHubServiceException(GitHubErrorCategory.Validation, "The selected repository does not expose a default branch.");

        return await SelectBranchAsync(defaultName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubFileInfo>> DiscoverWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var repository = RequireRepository();
        var branch = RequireBranch();
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        return await workflows.DiscoverFilesAsync(
            Owner(repository),
            RepositoryName(repository),
            branch.Name,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WorkflowDocumentState> LoadWorkflowAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidateWorkflowPath(path);
        var repository = RequireRepository();
        var branch = RequireBranch();
        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var file = await files.GetAsync(
            Owner(repository),
            RepositoryName(repository),
            path,
            branch.Name,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var model = parser.Parse(file.Content);
            var state = new WorkflowDocumentState(model, file.Path, branch.Name, file.Sha, false);
            Context.Workflow = state;
            return state;
        }
        catch (WorkflowYamlException)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public WorkflowDocumentState CreateWorkflow(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var branch = RequireBranch();
        var filename = name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{name}.yml";
        var path = $".github/workflows/{filename}";
        ValidateWorkflowPath(path);

        var model = new Workflow(Path.GetFileNameWithoutExtension(filename));
        var state = new WorkflowDocumentState(model, path, branch.Name, null, true);
        Context.Workflow = state;
        return state;
    }

    /// <inheritdoc />
    public async Task<WorkflowDocumentState> SaveWorkflowAsync(WorkflowDocumentState document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateWorkflowPath(document.Path);
        var repository = RequireRepository();
        var branch = RequireBranch();

        if (!string.Equals(document.Branch, branch.Name, StringComparison.Ordinal))
            throw new GitHubServiceException(GitHubErrorCategory.Validation, "The workflow branch does not match the selected branch.");

        await RequireAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var yaml = serializer.Serialize(document.Model);

        var request = new GitHubFileWriteRequest(
            document.Path,
            yaml,
            document.IsNew ? $"Create workflow {document.Path}" : $"Update workflow {document.Path}",
            branch.Name,
            document.IsNew ? null : RequireSha(document));

        var saved = await files.WriteAsync(
            Owner(repository),
            RepositoryName(repository),
            request,
            cancellationToken).ConfigureAwait(false);

        var updated = document with { BlobSha = saved.Sha, IsNew = false };
        Context.Workflow = updated;
        return updated;
    }

    private async Task RequireAuthenticatedAsync(CancellationToken cancellationToken)
    {
        var state = await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (state.State != GitHubAuthenticationState.Authenticated)
            throw new GitHubServiceException(
                state.State == GitHubAuthenticationState.InvalidCredential
                    ? GitHubErrorCategory.Authentication
                    : GitHubErrorCategory.Authorization,
                $"GitHub authentication is required. Current state: {state.State}.");
    }

    private GitHubRepositoryInfo RequireRepository() =>
        Context.Repository ?? throw new GitHubServiceException(GitHubErrorCategory.Validation, "Select a repository first.");

    private GitHubBranchInfo RequireBranch() =>
        Context.Branch ?? throw new GitHubServiceException(GitHubErrorCategory.Validation, "Select a branch first.");

    private static string RequireSha(WorkflowDocumentState document) =>
        !string.IsNullOrWhiteSpace(document.BlobSha)
            ? document.BlobSha
            : throw new GitHubServiceException(GitHubErrorCategory.Conflict, "An existing workflow is missing its current blob SHA.");

    private static string Owner(GitHubRepositoryInfo repository)
    {
        var fullName = repository.FullName;
        if (string.IsNullOrWhiteSpace(fullName))
            throw new GitHubServiceException(GitHubErrorCategory.Validation, "The repository does not expose its full name.");
        var separator = fullName.IndexOf('/');
        return separator > 0 ? fullName[..separator] : throw new GitHubServiceException(GitHubErrorCategory.Validation, "The repository owner could not be resolved.");
    }

    private static string RepositoryName(GitHubRepositoryInfo repository) =>
        !string.IsNullOrWhiteSpace(repository.Name)
            ? repository.Name
            : throw new GitHubServiceException(GitHubErrorCategory.Validation, "The repository name could not be resolved.");

    private static void ValidateWorkflowPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!WorkflowPathRegex().IsMatch(path))
            throw new GitHubServiceException(GitHubErrorCategory.Validation, "Workflow paths must be direct .yml or .yaml files under .github/workflows.");
        var filename = Path.GetFileName(path);
        if (filename is "." or ".." || filename.Contains('\') || filename.Contains('/'))
            throw new GitHubServiceException(GitHubErrorCategory.Validation, "The workflow filename is invalid.");
    }

    [GeneratedRegex(@"^\.github/workflows/[^/\\]+\.(?:yml|yaml)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WorkflowPathRegex();
}
