using GFlow.GitHub.Authentication;
using GFlow.GitHub.Contracts;
using GFlow.GitHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace GFlow.GitHub;

/// <summary>Registers GFlow GitHub infrastructure backed by the generated Kiota client.</summary>
public static class GitHubServiceCollectionExtensions
{
    /// <summary>Registers the generated GitHub client and GFlow-facing GitHub services with an existing adapter.</summary>
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

    /// <summary>Registers authentication, creates an authenticated Kiota adapter, and registers GitHub services.</summary>
    public static IServiceCollection AddGFlowGitHubAuthenticated(this IServiceCollection services)
    {
        services.AddGFlowGitHubAuthentication();
        services.AddSingleton(sp =>
            sp.GetRequiredService<IGitHubRequestAdapterFactory>().Create());
        services.AddSingleton<IGitHubRequestAdapter>(sp =>
            new GitHubRequestAdapter(sp.GetRequiredService<IRequestAdapter>()));
        services.AddSingleton(sp => new GitHubClient(sp.GetRequiredService<IRequestAdapter>()));
        services.AddScoped<IGitHubUserService, GitHubUserService>();
        services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
        services.AddScoped<IGitHubBranchService, GitHubBranchService>();
        services.AddScoped<IGitHubFileService, GitHubFileService>();
        services.AddScoped<IGitHubWorkflowService, GitHubWorkflowService>();
        services.AddScoped<IGitHubWorkflowRunService, GitHubWorkflowRunService>();
        return services;
    }

    /// <summary>Registers GFlow GitHub authentication and secure credential abstractions.</summary>
    public static IServiceCollection AddGFlowGitHubAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<GitHubAuthenticationService>();
        services.AddSingleton<IGitHubCredentialProvider>(sp => sp.GetRequiredService<GitHubAuthenticationService>());
        services.AddSingleton<IGitHubAuthenticationService>(sp => sp.GetRequiredService<GitHubAuthenticationService>());
        services.AddSingleton<IGitHubPermissionValidator, GitHubPermissionValidator>();
        services.AddSingleton<GitHubPatAuthenticationProvider>();
        services.AddSingleton<IAuthenticationProvider>(sp => sp.GetRequiredService<GitHubPatAuthenticationProvider>());
        services.AddSingleton<IGitHubRequestAdapterFactory, GitHubRequestAdapterFactory>();
        return services;
    }
}
