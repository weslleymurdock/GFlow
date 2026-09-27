using GFlow.GitHub.Services;

namespace GFlow.GitHub.Authentication;

/// <summary>Coordinates PAT persistence, validation, account resolution, and authentication state.</summary>
public sealed class GitHubAuthenticationService(
    ISecureCredentialStore credentialStore,
    IGitHubUserService userService,
    IGitHubPermissionValidator permissionValidator) : IGitHubAuthenticationService, IGitHubCredentialProvider
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
            Guid.NewGuid().ToString("N"),
            type,
            label,
            null,
            DateTimeOffset.UtcNow);

        await credentialStore.SaveAsync(
            GitHubCredentialSecret.Create(credential.Id, token),
            cancellationToken).ConfigureAwait(false);
        await credentialStore.SetActiveAsync(credential.Id, cancellationToken).ConfigureAwait(false);

        try
        {
            var user = await userService.GetAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
            var account = new GitHubAccount(user.Id, user.Login, user.Name, user.HtmlUrl);
            var updatedCredential = credential with { Login = user.Login };

            _state = new GitHubAuthenticationResult(
                GitHubAuthenticationState.Authenticated,
                updatedCredential,
                account,
                []);

            return _state;
        }
        catch (GitHubServiceException ex) when (ex.Category == Contracts.GitHubErrorCategory.Authentication)
        {
            await credentialStore.RemoveAsync(credential.Id, cancellationToken).ConfigureAwait(false);
            _state = new GitHubAuthenticationResult(GitHubAuthenticationState.InvalidCredential, null, null, []);
            return _state;
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
        catch (GitHubServiceException ex) when (ex.Category == Contracts.GitHubErrorCategory.Authentication)
        {
            _state = new GitHubAuthenticationResult(GitHubAuthenticationState.InvalidCredential, null, null, []);
            return _state;
        }
    }

    /// <inheritdoc />
    public GitHubAuthenticationResult GetCurrentState() => _state;

    /// <inheritdoc />
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var active = await credentialStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (active is not null)
            await credentialStore.RemoveAsync(active.CredentialId, cancellationToken).ConfigureAwait(false);

        _state = new GitHubAuthenticationResult(GitHubAuthenticationState.MissingCredential, null, null, []);
    }

    /// <inheritdoc />
    public Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default) =>
        credentialStore.GetActiveAsync(cancellationToken);

    /// <summary>Detects the PAT generation from GitHub's documented token prefixes.</summary>
    public static GitHubCredentialType DetectCredentialType(string token) =>
        token.StartsWith("github_pat_", StringComparison.Ordinal)
            ? GitHubCredentialType.FineGrainedPersonalAccessToken
            : GitHubCredentialType.ClassicPersonalAccessToken;
}
