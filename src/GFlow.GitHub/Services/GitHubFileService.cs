using GFlow.GitHub.Contracts;
using GFlow.GitHub.Repos.Item.Item.Contents.Item;
using System.Text;

namespace GFlow.GitHub.Services;

/// <summary>Provides repository file retrieval and write operations.</summary>
public interface IGitHubFileService
{
    /// <summary>Retrieves a repository file and its current blob SHA.</summary>
    Task<GitHubFileInfo> GetAsync(string owner, string repository, string path, string? reference = null, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a repository file. Updates preserve the supplied current blob SHA.</summary>
    Task<GitHubFileInfo> WriteAsync(string owner, string repository, GitHubFileWriteRequest request, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class GitHubFileService(GitHubClient client) : GitHubServiceBase, IGitHubFileService
{
    /// <inheritdoc />
    public async Task<GitHubFileInfo> GetAsync(string owner, string repository, string path, string? reference = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var result = await ExecuteAsync(async () =>
        {
            using var body = new MemoryStream();
            return await client.Repos[owner][repository].Contents[path].GetAsWithPathGetResponseAsync(
                body,
                config => config.QueryParameters.Ref = reference,
                cancellationToken);
        });

        var file = result?.ContentFile ?? throw new GitHubServiceException(
            GitHubErrorCategory.Validation,
            $"GitHub content '{path}' was not a file.");

        var content = Decode(file.Content, file.Encoding);
        return new GitHubFileInfo(file.Path ?? path, content, file.Sha ?? string.Empty, file.HtmlUrl);
    }

    /// <inheritdoc />
    public async Task<GitHubFileInfo> WriteAsync(string owner, string repository, GitHubFileWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Path);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommitMessage);

        var body = new WithPathPutRequestBody
        {
            Branch = request.Branch,
            Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Content)),
            Message = request.CommitMessage,
            Sha = request.Sha
        };

        var result = await ExecuteAsync(() => client.Repos[owner][repository].Contents[request.Path].PutAsync(body, cancellationToken: cancellationToken));
        var content = result?.Content;
        var commitSha = result?.Commit?.Sha ?? string.Empty;

        return new GitHubFileInfo(request.Path, request.Content, content?.Sha ?? commitSha, content?.HtmlUrl);
    }

    private static string Decode(string? content, string? encoding)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        if (!string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
            return content;

        return Encoding.UTF8.GetString(Convert.FromBase64String(content.Replace("\n", string.Empty)));
    }
}
