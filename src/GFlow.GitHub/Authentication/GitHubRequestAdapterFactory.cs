using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace GFlow.GitHub.Authentication;

/// <summary>Creates Kiota request adapters that obtain credentials from the GFlow authentication boundary.</summary>
public interface IGitHubRequestAdapterFactory
{
    /// <summary>Creates an authenticated request adapter.</summary>
    IRequestAdapter Create();
}

/// <inheritdoc />
public sealed class GitHubRequestAdapterFactory(IAuthenticationProvider authenticationProvider) : IGitHubRequestAdapterFactory
{
    /// <inheritdoc />
    public IRequestAdapter Create()
    {
        return new HttpClientRequestAdapter(authenticationProvider)
        {
            BaseUrl = "https://api.github.com"
        };
    }
}
