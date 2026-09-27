using GFlow.GitHub.Contracts;

namespace GFlow.GitHub.Services;

/// <summary>Provides repository branch/ref discovery operations.</summary>
public interface IGitHubBranchService
{
    /// <summary>Lists branches for a repository page.</summary>
    Task<IReadOnlyList<GitHubBranchInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubBranchService(GitHubClient client) : GitHubServiceBase, IGitHubBranchService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubBranchInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(perPage, 1);

        var branches = await ExecuteAsync(() => client.Repos[owner][repository].Branches.GetAsync(config =>
        {
            config.QueryParameters.Page = page;
            config.QueryParameters.PerPage = perPage;
        }, cancellationToken));

        return branches?.Select(branch => new GitHubBranchInfo(branch.Name ?? string.Empty, branch.Commit?.Sha)).ToArray() ?? [];
    }
}
