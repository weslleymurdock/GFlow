using GFlow.GitHub.Contracts;

namespace GFlow.GitHub.Authentication;

/// <summary>Identifies the supported GitHub credential mechanism.</summary>
public enum GitHubCredentialType
{
    /// <summary>A classic GitHub personal access token using OAuth-style scopes.</summary>
    ClassicPersonalAccessToken,

    /// <summary>A fine-grained GitHub personal access token using repository and account permissions.</summary>
    FineGrainedPersonalAccessToken
}

/// <summary>Describes the non-secret metadata of a stored GitHub credential.</summary>
public sealed record GitHubCredentialInfo(
    string Id,
    GitHubCredentialType Type,
    string? Label,
    string? Login,
    DateTimeOffset CreatedAt);

/// <summary>Represents an authenticated GitHub account without credential material.</summary>
public sealed record GitHubAccount(
    long? Id,
    string? Login,
    string? Name,
    string? HtmlUrl);

/// <summary>Represents the current authentication state.</summary>
public enum GitHubAuthenticationState
{
    /// <summary>No credential is configured.</summary>
    MissingCredential,

    /// <summary>A credential is available but has not been validated.</summary>
    Unvalidated,

    /// <summary>The credential is valid and the authenticated account is known.</summary>
    Authenticated,

    /// <summary>The credential is invalid or has been revoked.</summary>
    InvalidCredential,

    /// <summary>The credential is valid but required permissions are not available.</summary>
    InsufficientPermissions
}

/// <summary>Represents the result of validating one required GitHub capability.</summary>
public sealed record GitHubPermissionResult(
    string Capability,
    bool Satisfied,
    string? MissingPermission = null,
    string? Detail = null);

/// <summary>Represents the complete authentication validation result.</summary>
public sealed record GitHubAuthenticationResult(
    GitHubAuthenticationState State,
    GitHubCredentialInfo? Credential,
    GitHubAccount? Account,
    IReadOnlyList<GitHubPermissionResult> Permissions)
{
    /// <summary>Gets whether all explicitly requested permissions are satisfied.</summary>
    public bool HasRequiredPermissions => Permissions.All(static permission => permission.Satisfied);
}

/// <summary>Contains a PAT secret and is intentionally restricted to authentication infrastructure.</summary>
public sealed record GitHubCredentialSecret(string CredentialId, string Token)
{
    /// <summary>Creates a PAT secret after validating that the token is not empty.</summary>
    public static GitHubCredentialSecret Create(string credentialId, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new GitHubCredentialSecret(credentialId, token);
    }
}

/// <summary>Provides secure persistence for authentication secrets.</summary>
public interface ISecureCredentialStore
{
    /// <summary>Saves a credential secret.</summary>
    Task SaveAsync(GitHubCredentialSecret credential, CancellationToken cancellationToken = default);

    /// <summary>Loads the active credential secret.</summary>
    Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a credential secret by identifier.</summary>
    Task<bool> RemoveAsync(string credentialId, CancellationToken cancellationToken = default);

    /// <summary>Selects the credential identifier as the active credential.</summary>
    Task SetActiveAsync(string credentialId, CancellationToken cancellationToken = default);
}

/// <summary>Provides GitHub authentication/session operations to the application.</summary>
public interface IGitHubAuthenticationService
{
    /// <summary>Adds and validates a personal access token.</summary>
    Task<GitHubAuthenticationResult> AuthenticateAsync(
        string token,
        string? label = null,
        GitHubCredentialType? credentialType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Validates the active credential.</summary>
    Task<GitHubAuthenticationResult> ValidateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current authentication result without performing network validation.</summary>
    GitHubAuthenticationResult GetCurrentState();

    /// <summary>Removes the active credential and clears the authentication session.</summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);
}

/// <summary>Supplies the active credential to the Kiota authentication provider.</summary>
public interface IGitHubCredentialProvider
{
    /// <summary>Gets the active credential secret.</summary>
    Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Defines a GitHub permission requirement.</summary>
public sealed record GitHubPermissionRequirement(
    string Capability,
    string? ClassicScope,
    string FineGrainedPermission,
    GitHubPermissionAccess Access);

/// <summary>Defines the access level required from a fine-grained repository permission.</summary>
public enum GitHubPermissionAccess
{
    /// <summary>Read access is sufficient.</summary>
    Read,

    /// <summary>Write access is required.</summary>
    Write
}

/// <summary>Validates explicitly modeled GitHub permission evidence.</summary>
public interface IGitHubPermissionValidator
{
    /// <summary>Validates requirements against token permission evidence.</summary>
    IReadOnlyList<GitHubPermissionResult> Validate(
        GitHubCredentialType credentialType,
        IReadOnlySet<string> grantedPermissions,
        IEnumerable<GitHubPermissionRequirement> requirements);
}

/// <summary>Provides permission evidence captured by the GitHub API integration.</summary>
public sealed record GitHubPermissionEvidence(
    GitHubCredentialType CredentialType,
    IReadOnlySet<string> GrantedPermissions);
