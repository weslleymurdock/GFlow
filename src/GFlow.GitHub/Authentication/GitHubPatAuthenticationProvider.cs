using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GFlow.GitHub.Authentication;

/// <summary>Authenticates Kiota requests with the currently active GitHub PAT.</summary>
public sealed class GitHubPatAuthenticationProvider(IGitHubCredentialProvider credentialProvider) : IAuthenticationProvider
{
    /// <inheritdoc />
    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var credential = await credentialProvider.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (credential is null)
            throw new GitHubAuthenticationException("No active GitHub credential is configured.");

        request.Headers.TryAdd("Authorization", $"Bearer {credential.Token}");
        request.Headers.TryAdd("Accept", "application/vnd.github+json");
        request.Headers.TryAdd("X-GitHub-Api-Version", "2026-03-10");
    }
}

/// <summary>Represents a local authentication failure before a GitHub request is sent.</summary>
public sealed class GitHubAuthenticationException(string message) : Exception(message);
