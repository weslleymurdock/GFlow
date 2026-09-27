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
        RegisterGitHubServices(services, requestAdapter);
        return services;
    }

    /// <summary>Registers authentication and the generated GitHub client without introducing a dependency cycle.</summary>
    public static IServiceCollection AddGFlowGitHubAuthenticated(this IServiceCollection services)
    {
        services.AddGFlowGitHubAuthentication();
        services.AddSingleton<IRequestAdapter>(sp => sp.GetRequiredService<IGitHubRequestAdapterFactory>().Create());
        services.AddSingleton<GitHubClient>(sp => new GitHubClient(sp.GetRequiredService<IRequestAdapter>()));
        services.AddSingleton<IGitHubRequestAdapter>(sp => new GitHubRequestAdapter(sp.GetRequiredService<IRequestAdapter>()));
        RegisterGitHubServiceContracts(services);
        return services;
    }

    /// <summary>Registers GitHub authentication and secure credential abstractions.</summary>
    public static IServiceCollection AddGFlowGitHubAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<GitHubSecureCredentialProvider>();
        services.AddSingleton<IGitHubCredentialProvider>(sp => sp.GetRequiredService<GitHubSecureCredentialProvider>());
        services.AddScoped<IGitHubAuthenticationService, GitHubAuthenticationService>();
        services.AddSingleton<IGitHubPermissionValidator, GitHubPermissionValidator>();
        services.AddSingleton<GitHubPatAuthenticationProvider>();
        services.AddSingleton<IAuthenticationProvider>(sp => sp.GetRequiredService<GitHubPatAuthenticationProvider>());
        services.AddSingleton<IGitHubRequestAdapterFactory, GitHubRequestAdapterFactory>();
        return services;
    }

    private static void RegisterGitHubServices(IServiceCollection services, IRequestAdapter requestAdapter)
    {
        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(IRequestAdapter)))
            services.AddSingleton(requestAdapter);

        services.AddSingleton<GitHubClient>(sp => new GitHubClient(sp.GetRequiredService<IRequestAdapter>()));
        services.AddSingleton<IGitHubRequestAdapter>(sp => new GitHubRequestAdapter(sp.GetRequiredService<IRequestAdapter>()));
        RegisterGitHubServiceContracts(services);
    }

    private static void RegisterGitHubServiceContracts(IServiceCollection services)
    {
        services.AddScoped<IGitHubUserService, GitHubUserService>();
        services.AddScoped<IGitHubRepositoryService, GitHubRepositoryService>();
        services.AddScoped<IGitHubBranchService, GitHubBranchService>();
        services.AddScoped<IGitHubFileService, GitHubFileService>();
        services.AddScoped<IGitHubWorkflowService, GitHubWorkflowService>();
        services.AddScoped<IGitHubWorkflowRunService, GitHubWorkflowRunService>();
    }
}
