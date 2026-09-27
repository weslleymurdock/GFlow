using GFlow.GitHub.Authentication;
using Microsoft.Maui.Storage;

namespace GFlow.Infrastructure.Security;

/// <summary>Persists GFlow GitHub credentials exclusively through .NET MAUI SecureStorage.</summary>
public sealed class MauiSecureCredentialStore(ISecureStorage secureStorage) : ISecureCredentialStore
{
    private const string ActiveCredentialKey = "gflow.github.active-credential";
    private const string CredentialPrefix = "gflow.github.credential.";

    /// <inheritdoc />
    public async Task SaveAsync(GitHubCredentialSecret credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        cancellationToken.ThrowIfCancellationRequested();

        await secureStorage.SetAsync(CredentialPrefix + credential.CredentialId, credential.Token).ConfigureAwait(false);
        await secureStorage.SetAsync(ActiveCredentialKey, credential.CredentialId).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GitHubCredentialSecret?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var id = await secureStorage.GetAsync(ActiveCredentialKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var token = await secureStorage.GetAsync(CredentialPrefix + id).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(token) ? null : GitHubCredentialSecret.Create(id, token);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        cancellationToken.ThrowIfCancellationRequested();

        var active = await secureStorage.GetAsync(ActiveCredentialKey).ConfigureAwait(false);
        var removed = secureStorage.Remove(CredentialPrefix + credentialId);

        if (string.Equals(active, credentialId, StringComparison.Ordinal))
            secureStorage.Remove(ActiveCredentialKey);

        return removed;
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        cancellationToken.ThrowIfCancellationRequested();
        await secureStorage.SetAsync(ActiveCredentialKey, credentialId).ConfigureAwait(false);
    }
}
