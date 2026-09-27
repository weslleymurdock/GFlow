using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;

namespace GFlow.GitHub;

/// <summary>Registers GFlow GitHub infrastructure backed by the generated Kiota client.</summary>
public static class GitHubServiceCollectionExtensions
{
    /// <summary>Registers the generated GitHub client and GFlow-facing GitHub services.</summary>
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

    /// <summary>Registers GFlow GitHub authentication and secure credential services.</summary>
    public static IServiceCollection AddGFlowGitHubAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<ISecureCredentialStore, GFlow.Infrastructure.Security.MauiSecureCredentialStore>();
        services.AddSingleton<IGitHubCredentialProvider, GitHubAuthenticationService>();
        services.AddSingleton<IGitHubAuthenticationService, GitHubAuthenticationService>();
        services.AddSingleton<IGitHubPermissionValidator, GitHubPermissionValidator>();
        services.AddSingleton<GitHubPatAuthenticationProvider>();
        services.AddSingleton<IAuthenticationProvider>(sp => sp.GetRequiredService<GitHubPatAuthenticationProvider>());
        services.AddSingleton<IGitHubRequestAdapterFactory, GitHubRequestAdapterFactory>();
        return services;
    }
}
