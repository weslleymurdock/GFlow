namespace GFlow.GitHub.Authentication;

/// <summary>Defines the minimum permission requirements used by the current GFlow GitHub services.</summary>
public static class GitHubPermissionRequirements
{
    /// <summary>Reads the authenticated user.</summary>
    public static GitHubPermissionRequirement AuthenticatedUser { get; } =
        new("authenticated-user", "read:user", "user", GitHubPermissionAccess.Read);

    /// <summary>Reads repository metadata.</summary>
    public static GitHubPermissionRequirement RepositoryMetadataRead { get; } =
        new("repository-metadata-read", "repo", "metadata", GitHubPermissionAccess.Read);

    /// <summary>Reads repository contents.</summary>
    public static GitHubPermissionRequirement RepositoryContentsRead { get; } =
        new("repository-contents-read", "repo", "contents", GitHubPermissionAccess.Read);

    /// <summary>Writes repository contents.</summary>
    public static GitHubPermissionRequirement RepositoryContentsWrite { get; } =
        new("repository-contents-write", "repo", "contents", GitHubPermissionAccess.Write);

    /// <summary>Creates a repository for the authenticated account.</summary>
    public static GitHubPermissionRequirement RepositoryCreation { get; } =
        new("repository-creation", "repo", "administration/repository-creation", GitHubPermissionAccess.Write);

    /// <summary>Reads GitHub Actions workflow and run information.</summary>
    public static GitHubPermissionRequirement ActionsRead { get; } =
        new("actions-read", "repo", "actions", GitHubPermissionAccess.Read);

    /// <summary>Dispatches GitHub Actions workflows.</summary>
    public static GitHubPermissionRequirement ActionsWrite { get; } =
        new("actions-write", "repo", "actions", GitHubPermissionAccess.Write);

    /// <summary>Returns the minimum set used by the repository/workflow product loop.</summary>
    public static IReadOnlyList<GitHubPermissionRequirement> ProductLoop { get; } =
    [
        AuthenticatedUser,
        RepositoryMetadataRead,
        RepositoryContentsRead,
        RepositoryContentsWrite,
        RepositoryCreation,
        ActionsRead,
        ActionsWrite
    ];
}
