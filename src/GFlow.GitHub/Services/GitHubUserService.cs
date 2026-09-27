using GFlow.GitHub.Contracts;

namespace GFlow.GitHub.Services;

/// <summary>Provides authenticated-user operations backed by the generated Kiota client.</summary>
public interface IGitHubUserService
{
    /// <summary>Gets the authenticated GitHub user.</summary>
    Task<GitHubUserInfo> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubUserService(GitHubClient client) : GitHubServiceBase, IGitHubUserService
{
    /// <inheritdoc />
    public async Task<GitHubUserInfo> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteAsync(() => client.User.GetAsUserGetResponseAsync(cancellationToken: cancellationToken));
        var user = response?.PrivateUser ?? response?.PublicUser;
        return new(user?.Id, user?.Login, user?.Name, user?.HtmlUrl);
    }
}
