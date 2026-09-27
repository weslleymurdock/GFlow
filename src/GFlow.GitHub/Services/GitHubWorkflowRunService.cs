using GFlow.GitHub.Contracts;

namespace GFlow.GitHub.Services;

/// <summary>Provides GitHub Actions workflow run retrieval and cancellation.</summary>
public interface IGitHubWorkflowRunService
{
    /// <summary>Gets a workflow run by identifier.</summary>
    Task<GitHubWorkflowRunInfo> GetAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default);

    /// <summary>Lists workflow runs for a repository page.</summary>
    Task<IReadOnlyList<GitHubWorkflowRunInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, string? branch = null, string? status = null, CancellationToken cancellationToken = default);

    /// <summary>Cancels a workflow run.</summary>
    Task CancelAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubWorkflowRunService(GitHubClient client) : GitHubServiceBase, IGitHubWorkflowRunService
{
    /// <inheritdoc />
    public async Task<GitHubWorkflowRunInfo> GetAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default)
    {
        Validate(owner, repository, runId);
        var result = await ExecuteAsync(() => client.Repos[owner][repository].Actions.Runs[(int)runId].GetAsync(cancellationToken: cancellationToken));
        return Map(result);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubWorkflowRunInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, string? branch = null, string? status = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(perPage, 1);

        var result = await ExecuteAsync(() => client.Repos[owner][repository].Actions.Runs.GetAsRunsGetResponseAsync(config =>
        {
            config.QueryParameters.Page = page;
            config.QueryParameters.PerPage = perPage;
            config.QueryParameters.Branch = branch;
            config.QueryParameters.Status = status;
        }, cancellationToken));

        return result?.WorkflowRuns?.Select(Map).ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task CancelAsync(string owner, string repository, long runId, CancellationToken cancellationToken = default)
    {
        Validate(owner, repository, runId);
        await ExecuteAsync(() => client.Repos[owner][repository].Actions.Runs[(int)runId].Cancel.PostAsync(cancellationToken: cancellationToken));
    }

    private static void Validate(string owner, string repository, long runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(runId, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(runId, int.MaxValue);
    }

    private static GitHubWorkflowRunInfo Map(GFlow.GitHub.Models.WorkflowRun? run) =>
        new(run?.Id, run?.Name, run?.Status, run?.Conclusion, run?.HeadBranch, run?.HeadSha, run?.HtmlUrl, run?.CreatedAt, run?.UpdatedAt);
}
