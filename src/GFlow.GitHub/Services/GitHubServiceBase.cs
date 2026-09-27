using GFlow.GitHub.Contracts;
using Microsoft.Kiota.Abstractions;

namespace GFlow.GitHub.Services;

/// <summary>Provides common exception mapping and execution helpers for GitHub services.</summary>
public abstract class GitHubServiceBase
{
    /// <summary>Maps a GitHub or transport exception to an application-facing exception.</summary>
    /// <param name="exception">The exception raised by the GitHub operation.</param>
    /// <returns>A mapped <see cref="GitHubServiceException"/>.</returns>
    public static GitHubServiceException MapException(Exception exception)
    {
        if (exception is OperationCanceledException)
            return new GitHubServiceException(GitHubErrorCategory.Cancellation, "The GitHub operation was cancelled.", null, exception);

        if (exception is ApiException apiException)
        {
            var status = apiException.ResponseStatusCode;
            var category = status switch
            {
                401 => GitHubErrorCategory.Authentication,
                403 => GitHubErrorCategory.Authorization,
                404 => GitHubErrorCategory.NotFound,
                409 => GitHubErrorCategory.Conflict,
                422 => GitHubErrorCategory.Validation,
                429 => GitHubErrorCategory.RateLimit,
                >= 500 and <= 599 => GitHubErrorCategory.Server,
                _ => GitHubErrorCategory.Unknown
            };

            return new GitHubServiceException(
                category,
                $"GitHub returned HTTP {(int)status}.",
                (int)status,
                exception);
        }

        return new GitHubServiceException(GitHubErrorCategory.Unknown, "The GitHub operation failed.", null, exception);
    }

    /// <summary>Executes an asynchronous GitHub operation and maps failures.</summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <returns>The operation result.</returns>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (GitHubServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }

    /// <summary>Executes an asynchronous GitHub operation that has no result and maps failures.</summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    public static async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (GitHubServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw MapException(ex);
        }
    }
}
