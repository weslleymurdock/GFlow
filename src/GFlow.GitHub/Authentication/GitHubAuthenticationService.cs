using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;

namespace GFlow.GitHub.Authentication;

/// <summary>Coordinates PAT persistence, validation, account resolution, and authentication state.</summary>
public sealed class GitHubAuthenticationService(
    ISecureCredentialStore credentialStore,
    IGitHubUserService userService,
    IGitHubEffectivePermissionValidator effectivePermissionValidator) : IGitHubAuthenticationService
{
    private GitHubAuthenticationResult _state =
        new(GitHubAuthenticationState.MissingCredential, null, null, []);

    /// <inheritdoc />
    public async Task<GitHubAuthenticationResult> AuthenticateAsync(
        string token,
        string? label = null,
        GitHubCredentialType? credentialType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var type = credentialType ?? DetectCredentialType(token);
        var credential = new GitHubCredentialInfo(
            Guid.NewGuid().ToString("N"), type, label, null, DateTimeOffset.UtcNow);

        await credentialStore.SaveAsync(
            GitHubCredentialSecret.Create(credential.Id, token), cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await ValidateAsync(cancellationToken).ConfigureAwait(false);
            if (result.State == GitHubAuthenticationState.InvalidCredential)
            {
                await credentialStore.RemoveAsync(credential.Id, cancellationToken).ConfigureAwait(false);
                return result;
            }

            if (result.Credential is null)
                return result with { Credential = credential };

            return result;
        }
        catch
        {
            await credentialStore.RemoveAsync(credential.Id, cancellationToken).ConfigureAwait(false);
            _state = new GitHubAuthenticationResult(GitHubAuthenticationState.InvalidCredential, null, null, []);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<GitHubAuthenticationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var secret = await credentialStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            _state = new GitHubAuthenticationResult(GitHubAuthenticationState.MissingCredential, null, null, []);
            return _state;
        }

        try
        {
            var user = await userService.GetAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
            var account = new GitHubAccount(user.Id, user.Login, user.Name, user.HtmlUrl);
            var type = DetectCredentialType(secret.Token);
            var credential = _state.Credential?.Id == secret.CredentialId
                ? _state.Credential
                : new GitHubCredentialInfo(secret.CredentialId, type, null, user.Login, DateTimeOffset.UtcNow);

            _state = new GitHubAuthenticationResult(
                GitHubAuthenticationState.Authenticated,
                credential,
                account,
                _state.Permissions);

            return _state;
        }
        catch (GitHubServiceException ex) when (ex.Category == GitHubErrorCategory.Authentication)
        {
            _state = new GitHubAuthenticationResult(GitHubAuthenticationState.InvalidCredential, null, null, []);
            return _state;
        }
    }

    /// <inheritdoc />
    public GitHubAuthenticationResult GetCurrentState() => _state;

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubPermissionResult>> ValidatePermissionsAsync(
        string? owner = null,
        string? repository = null,
        CancellationToken cancellationToken = default)
    {
        var state = await ValidateAsync(cancellationToken).ConfigureAwait(false);
        if (state.Credential is null)
            return GitHubPermissionRequirements.ProductLoop
                .Select(requirement => new GitHubPermissionResult(
                    requirement.Capability,
                    false,
                    state.State == GitHubAuthenticationState.InvalidCredential
                        ? requirement.ClassicScope
                        : requirement.FineGrainedPermission,
                    $"Authentication state is {state.State}.",
                    true))
                .ToArray();

        var permissions = await effectivePermissionValidator.ValidateAsync(
            state.Credential.Type,
            owner,
            repository,
            cancellationToken).ConfigureAwait(false);

        _state = state with
        {
            State = permissions.Any(static permission => permission.Verified && !permission.Satisfied)
                ? GitHubAuthenticationState.InsufficientPermissions
                : GitHubAuthenticationState.Authenticated,
            Permissions = permissions
        };

        return permissions;
    }

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var active = await credentialStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (active is not null)
            await credentialStore.RemoveAsync(active.CredentialId, cancellationToken).ConfigureAwait(false);

        _state = new GitHubAuthenticationResult(GitHubAuthenticationState.MissingCredential, null, null, []);
    }

    /// <summary>Detects the PAT generation from GitHub's documented token prefixes.</summary>
    public static GitHubCredentialType DetectCredentialType(string token) =>
        token.StartsWith("github_pat_", StringComparison.Ordinal)
            ? GitHubCredentialType.FineGrainedPersonalAccessToken
            : GitHubCredentialType.ClassicPersonalAccessToken;
}
