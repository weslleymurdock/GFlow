using GFlow.GitHub.Contracts;
using GFlow.GitHub.Repos.Item.Item.Actions.Workflows.Item.Dispatches;

namespace GFlow.GitHub.Services;

/// <summary>Provides GitHub Actions workflow discovery, dispatch, and workflow-file operations.</summary>
public interface IGitHubWorkflowService
{
    /// <summary>Lists workflows in a repository page.</summary>
    Task<IReadOnlyList<GitHubWorkflowInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, CancellationToken cancellationToken = default);

    /// <summary>Gets a workflow by numeric identifier or workflow file name.</summary>
    Task<GitHubWorkflowInfo> GetAsync(string owner, string repository, string workflow, CancellationToken cancellationToken = default);

    /// <summary>Discovers workflow files under the repository's .github/workflows directory.</summary>
    Task<IReadOnlyList<GitHubFileInfo>> DiscoverFilesAsync(string owner, string repository, string? reference = null, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a workflow on a branch or tag reference.</summary>
    Task<GitHubWorkflowRunInfo?> DispatchAsync(string owner, string repository, GitHubWorkflowDispatchRequest request, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubWorkflowService(GitHubClient client, IGitHubFileService fileService) : GitHubServiceBase, IGitHubWorkflowService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubWorkflowInfo>> ListAsync(string owner, string repository, int page = 1, int perPage = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(perPage, 1);

        var result = await ExecuteAsync(() => client.Repos[owner][repository].Actions.Workflows.GetAsWorkflowsGetResponseAsync(config =>
        {
            config.QueryParameters.Page = page;
            config.QueryParameters.PerPage = perPage;
        }, cancellationToken));

        return result?.Workflows?.Select(Map).ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task<GitHubWorkflowInfo> GetAsync(string owner, string repository, string workflow, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflow);

        var result = await ExecuteAsync(() => client.Repos[owner][repository].Actions.Workflows[workflow].GetAsync(cancellationToken: cancellationToken));
        return Map(result);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubFileInfo>> DiscoverFilesAsync(string owner, string repository, string? reference = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        // The contents endpoint is authoritative for repository files. A workflow directory
        // contains only the files that are actually persisted in .github/workflows.
        var result = await ExecuteAsync(async () =>
        {
            using var body = new MemoryStream();
            return await client.Repos[owner][repository].Contents[".github/workflows"].GetAsWithPathGetResponseAsync(
                body,
                config => config.QueryParameters.Ref = reference,
                cancellationToken);
        });

        if (result?.ContentFile is not null)
            return [MapFile(result.ContentFile)];

        var entries = result?.ContentDirectory ?? [];
        var workflowFiles = entries
            .Where(entry => string.Equals(entry.Type?.ToString(), "File", StringComparison.OrdinalIgnoreCase)
                && (entry.Name?.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) == true
                    || entry.Name?.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) == true))
            .Select(entry => new GitHubFileInfo(entry.Path ?? entry.Name ?? string.Empty, string.Empty, entry.Sha ?? string.Empty, entry.HtmlUrl))
            .ToArray();

        return workflowFiles;
    }

    /// <inheritdoc />
    public async Task<GitHubWorkflowRunInfo?> DispatchAsync(string owner, string repository, GitHubWorkflowDispatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Ref);

        var body = new DispatchesPostRequestBody
        {
            Ref = request.Ref
        };

        if (request.Inputs is { Count: > 0 })
        {
            var inputs = new DispatchesPostRequestBody_inputs();
            foreach (var pair in request.Inputs)
                inputs.AdditionalData[pair.Key] = pair.Value;
            body.Inputs = inputs;
        }

        var result = await ExecuteAsync(() => client.Repos[owner][repository].Actions.Workflows[request.Workflow].Dispatches.PostAsync(body, cancellationToken: cancellationToken));
        if (result?.WorkflowRunId is null)
            return null;

        return new GitHubWorkflowRunInfo(result.WorkflowRunId, null, null, null, request.Ref, null, result.HtmlUrl, null, null);
    }

    private static GitHubWorkflowInfo Map(GFlow.GitHub.Models.Workflow? workflow) =>
        new(workflow?.Id, workflow?.Name, workflow?.Path, workflow?.State?.ToString(), workflow?.HtmlUrl);

    private static GitHubFileInfo MapFile(GFlow.GitHub.Models.ContentFile file) =>
        new(file.Path ?? file.Name ?? string.Empty, string.Empty, file.Sha ?? string.Empty, file.HtmlUrl);
}
