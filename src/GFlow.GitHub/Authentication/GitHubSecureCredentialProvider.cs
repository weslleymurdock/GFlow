namespace GFlow.GitHub.Authentication;

/// <summary>Supplies the active credential directly from secure storage to low-level authentication infrastructure.</summary>
public sealed class GitHubSecureCredentialProvider(ISecureCredentialStore credentialStore) : IGitHubCredentialProvider
{
    /// <inheritdoc />
    public Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default) =>
        credentialStore.GetActiveAsync(cancellationToken);
}
