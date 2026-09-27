using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;

namespace GFlow.GitHub.Authentication;

/// <summary>Builds permission evidence by exercising the existing authenticated GitHub service boundary.</summary>
public interface IGitHubEffectivePermissionValidator
{
    /// <summary>Validates permissions that can be proven safely for the selected repository.</summary>
    Task<IReadOnlyList<GitHubPermissionResult>> ValidateAsync(
        GitHubCredentialType credentialType,
        string? owner = null,
        string? repository = null,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubEffectivePermissionValidator(
    IGitHubUserService userService,
    IGitHubRepositoryService repositoryService,
    IGitHubWorkflowService workflowService,
    IGitHubPermissionValidator validator) : IGitHubEffectivePermissionValidator
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubPermissionResult>> ValidateAsync(
        GitHubCredentialType credentialType,
        string? owner = null,
        string? repository = null,
        CancellationToken cancellationToken = default)
    {
        var observations = new Dictionary<string, GitHubPermissionObservation>(StringComparer.Ordinal);

        try
        {
            await userService.GetAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
            observations[GitHubPermissionRequirements.AuthenticatedUser.Capability] =
                GitHubPermissionObservation.Verified("GET /user succeeded with the active credential.");
        }
        catch (GitHubServiceException ex) when (ex.Category is GitHubErrorCategory.Authorization or GitHubErrorCategory.Authentication)
        {
            observations[GitHubPermissionRequirements.AuthenticatedUser.Capability] =
                GitHubPermissionObservation.Missing(ex.Category.ToString());
        }

        if (!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repository))
        {
            try
            {
                var repo = await repositoryService.GetAsync(owner, repository, cancellationToken).ConfigureAwait(false);
                observations[GitHubPermissionRequirements.RepositoryMetadataRead.Capability] =
                    GitHubPermissionObservation.Verified("GET /repos/{owner}/{repo} succeeded.");

                try
                {
                    await workflowService.ListAsync(owner, repository, cancellationToken: cancellationToken).ConfigureAwait(false);
                    observations[GitHubPermissionRequirements.ActionsRead.Capability] =
                        GitHubPermissionObservation.Verified("GET /repos/{owner}/{repo}/actions/workflows succeeded.");

                    observations[GitHubPermissionRequirements.RepositoryContentsRead.Capability] =
                        repo.IsPrivate == true
                            ? GitHubPermissionObservation.Verified("Authenticated access to a private repository's workflow contents succeeded.")
                            : GitHubPermissionObservation.Unverified("The selected repository is public, so successful reads do not prove Contents permission.");
                }
                catch (GitHubServiceException ex) when (ex.Category == GitHubErrorCategory.Authorization)
                {
                    observations[GitHubPermissionRequirements.ActionsRead.Capability] =
                        GitHubPermissionObservation.Missing("The authenticated workflow-list request was forbidden.");
                    observations[GitHubPermissionRequirements.RepositoryContentsRead.Capability] =
                        GitHubPermissionObservation.Unverified("The workflow endpoint cannot isolate Contents permission.");
                }
            }
            catch (GitHubServiceException ex) when (ex.Category == GitHubErrorCategory.Authorization)
            {
                observations[GitHubPermissionRequirements.RepositoryMetadataRead.Capability] =
                    GitHubPermissionObservation.Missing("The authenticated repository request was forbidden.");
            }
        }

        observations[GitHubPermissionRequirements.RepositoryContentsWrite.Capability] =
            GitHubPermissionObservation.Unverified("A write permission cannot be safely proven without mutating repository state.");
        observations[GitHubPermissionRequirements.WorkflowFilesWrite.Capability] =
            GitHubPermissionObservation.Unverified("Workflow-file write permission cannot be safely proven without changing .github/workflows.");
        observations[GitHubPermissionRequirements.RepositoryCreation.Capability] =
            GitHubPermissionObservation.Unverified("Repository creation permission cannot be safely proven without creating a repository.");
        observations[GitHubPermissionRequirements.ActionsWrite.Capability] =
            GitHubPermissionObservation.Unverified("Actions write permission cannot be safely proven without dispatching or mutating workflow state.");

        return validator.ValidateEvidence(credentialType, observations);
    }
}
