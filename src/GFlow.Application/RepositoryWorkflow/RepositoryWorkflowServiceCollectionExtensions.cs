using Microsoft.Extensions.DependencyInjection;

namespace GFlow.Application.RepositoryWorkflow;

/// <summary>Registers repository and workflow management application services.</summary>
public static class RepositoryWorkflowServiceCollectionExtensions
{
    /// <summary>Registers the Stage 05 repository/workflow context and service.</summary>
    public static IServiceCollection AddRepositoryWorkflowManagement(this IServiceCollection services)
    {
        services.AddScoped<RepositoryWorkflowContext>();
        services.AddScoped<IRepositoryWorkflowService, RepositoryWorkflowService>();
        return services;
    }
}
