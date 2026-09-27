namespace GFlow.GitHub.Contracts;

/// <summary>Represents authenticated GitHub user information exposed by GFlow.</summary>
public sealed record GitHubUserInfo(long? Id, string? Login, string? Name, string? HtmlUrl);

/// <summary>Represents the repository information required by GFlow.</summary>
public sealed record GitHubRepositoryInfo(long? Id, string? Name, string? FullName, bool? IsPrivate, string? DefaultBranch, string? HtmlUrl)
{
    /// <summary>Gets the repository owner derived from the GitHub full name.</summary>
    public string? Owner => FullName?.IndexOf('/') is > 0 and var separator ? FullName[..separator] : null;
}

/// <summary>Represents a Git reference exposed by GFlow.</summary>
public sealed record GitHubBranchInfo(string Name, string? Sha);

/// <summary>Represents repository file content together with its blob SHA.</summary>
public sealed record GitHubFileInfo(string Path, string Content, string Sha, string? HtmlUrl);

/// <summary>Represents a GitHub Actions workflow.</summary>
public sealed record GitHubWorkflowInfo(long? Id, string? Name, string? Path, string? State, string? HtmlUrl);

/// <summary>Represents a GitHub Actions workflow run.</summary>
public sealed record GitHubWorkflowRunInfo(long? Id, string? Name, string? Status, string? Conclusion, string? Branch, string? Sha, string? HtmlUrl, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>Defines repository creation options.</summary>
public sealed record GitHubRepositoryCreateRequest(string Name, string? Description = null, bool IsPrivate = false, bool AutoInit = false);

/// <summary>Defines a repository file write operation. <see cref="Sha"/> is required when replacing an existing file.</summary>
public sealed record GitHubFileWriteRequest(string Path, string Content, string CommitMessage, string Branch, string? Sha = null);

/// <summary>Defines a workflow dispatch request.</summary>
public sealed record GitHubWorkflowDispatchRequest(string Workflow, string Ref, IReadOnlyDictionary<string, string>? Inputs = null);

/// <summary>Defines the supported GitHub service error categories.</summary>
public enum GitHubErrorCategory
{
    /// <summary>Authentication credentials are missing or invalid.</summary>
    Authentication,

    /// <summary>The authenticated identity is not authorized for the operation.</summary>
    Authorization,

    /// <summary>The requested GitHub resource does not exist.</summary>
    NotFound,

    /// <summary>The requested operation conflicts with the current GitHub resource state.</summary>
    Conflict,

    /// <summary>GitHub rejected the request because its data is invalid.</summary>
    Validation,

    /// <summary>The GitHub API rate limit has been exceeded.</summary>
    RateLimit,

    /// <summary>GitHub returned a server-side failure.</summary>
    Server,

    /// <summary>The asynchronous operation was cancelled.</summary>
    Cancellation,

    /// <summary>The failure does not match another supported category.</summary>
    Unknown
}

/// <summary>Represents an application-facing GitHub operation failure.</summary>
public sealed class GitHubServiceException : Exception
{
    /// <summary>Initializes a new GitHub service exception.</summary>
    public GitHubServiceException(GitHubErrorCategory category, string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException) => (Category, StatusCode) = (category, statusCode);

    /// <summary>Gets the application-facing error category.</summary>
    public GitHubErrorCategory Category { get; }

    /// <summary>Gets the HTTP status code when GitHub supplied one.</summary>
    public int? StatusCode { get; }
}

/// <summary>Provides the authenticated Kiota request adapter used by GFlow GitHub services.</summary>
public interface IGitHubRequestAdapter
{
    /// <summary>Gets the authenticated Kiota request adapter.</summary>
    Microsoft.Kiota.Abstractions.IRequestAdapter RequestAdapter { get; }
}

/// <summary>Provides the authenticated Kiota request adapter to the GitHub infrastructure.</summary>
public sealed class GitHubRequestAdapter : IGitHubRequestAdapter
{
    /// <summary>Initializes the adapter boundary.</summary>
    public GitHubRequestAdapter(Microsoft.Kiota.Abstractions.IRequestAdapter requestAdapter) =>
        RequestAdapter = requestAdapter ?? throw new ArgumentNullException(nameof(requestAdapter));

    /// <inheritdoc />
    public Microsoft.Kiota.Abstractions.IRequestAdapter RequestAdapter { get; }
}
