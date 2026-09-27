using GFlow.GitHub.Contracts;
using GFlow.GitHub.User.Repos;

namespace GFlow.GitHub.Services;

/// <summary>Provides repository listing, lookup, and creation operations.</summary>
public interface IGitHubRepositoryService
{
    /// <summary>Lists repositories accessible to the authenticated user.</summary>
    Task<IReadOnlyList<GitHubRepositoryInfo>> ListAsync(int page = 1, int perPage = 100, CancellationToken cancellationToken = default);

    /// <summary>Gets a repository by owner and name.</summary>
    Task<GitHubRepositoryInfo> GetAsync(string owner, string repository, CancellationToken cancellationToken = default);

    /// <summary>Creates a repository for the authenticated user.</summary>
    Task<GitHubRepositoryInfo> CreateAsync(GitHubRepositoryCreateRequest request, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubRepositoryService(GitHubClient client) : GitHubServiceBase, IGitHubRepositoryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubRepositoryInfo>> ListAsync(int page = 1, int perPage = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(perPage, 1);

        var repositories = await ExecuteAsync(() => client.User.Repos.GetAsync(config =>
            {
                config.QueryParameters.Page = page;
                config.QueryParameters.PerPage = perPage;
            }, cancellationToken));

        return repositories?.Select(Map).ToArray() ?? [];
    }

    /// <inheritdoc />
    public async Task<GitHubRepositoryInfo> GetAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var result = await ExecuteAsync(() => client.Repos[owner][repository].GetAsync(cancellationToken: cancellationToken));
        return Map(result);
    }

    /// <inheritdoc />
    public async Task<GitHubRepositoryInfo> CreateAsync(GitHubRepositoryCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var body = new ReposPostRequestBody
        {
            Name = request.Name,
            Description = request.Description,
            Private = request.IsPrivate,
            AutoInit = request.AutoInit
        };

        var result = await ExecuteAsync(() => client.User.Repos.PostAsync(body, cancellationToken: cancellationToken));
        return Map(result);
    }

    private static GitHubRepositoryInfo Map(GFlow.GitHub.Models.Repository? repository) =>
        new(repository?.Id, repository?.Name, repository?.FullName, repository?.Private, repository?.DefaultBranch, repository?.HtmlUrl);

    private static GitHubRepositoryInfo Map(GFlow.GitHub.Models.FullRepository? repository) =>
        new(repository?.Id, repository?.Name, repository?.FullName, repository?.Private, repository?.DefaultBranch, repository?.HtmlUrl);
}
