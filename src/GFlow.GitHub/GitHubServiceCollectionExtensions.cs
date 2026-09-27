using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;

namespace GFlow.GitHub;

/// <summary>Registers GFlow GitHub infrastructure backed by the generated Kiota client.</summary>
public static class GitHubServiceCollectionExtensions
{
    /// <summary>
    /// Registers the generated GitHub client and GFlow-facing GitHub services.
    /// Authentication is supplied by the caller through an already configured Kiota request adapter.
    /// </summary>
    /// <param name="services">The dependency injection service collection.</param>
    /// <param name="requestAdapter">An authenticated Kiota request adapter.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddGFlowGitHub(this IServiceCollection services, IRequestAdapter requestAdapter)
    {
        ArgumentNullException.ThrowIfNull(requestAdapter);

        services.AddSingleton<IGitHubRequestAdapter>(_ => new GitHubRequestAdapter(requestAdapter));
        services.AddSingleton(requestAdapter);
        services.AddSingleton(sp => new GitHubClient(sp.GetRequiredService<IRequestAdapter>()));

        services.AddScoped<IGitHubUserService, GitHubUserService>();
        services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
        services.AddScoped<IGitHubBranchService, GitHubBranchService>();
        services.AddScoped<IGitHubFileService, GitHubFileService>();
        services.AddScoped<IGitHubWorkflowService, GitHubWorkflowService>();
        services.AddScoped<IGitHubWorkflowRunService, GitHubWorkflowRunService>();

        return services;
    }
}
