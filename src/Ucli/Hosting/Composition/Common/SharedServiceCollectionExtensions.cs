using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.EnvironmentVariables;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Git;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Common;

/// <summary> Registers shared services reused across feature boundaries. </summary>
internal static class SharedServiceCollectionExtensions
{
    /// <summary> Registers shared services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliSharedServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEnvironmentVariableReader, ProcessEnvironmentVariableReader>();
        services.AddSingleton<IUcliConfigStore, UcliConfigStore>();
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IProjectLifecycleLockProvider, FileSystemProjectLifecycleLockProvider>();
        services.AddSingleton<IUnityExecutionModeDecisionService, UnityExecutionModeDecisionService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitCommandClient, GitCommandClient>();
        services.AddSingleton<IGitWorktreeListPorcelainParser, GitWorktreeListPorcelainParser>();
        services.AddSingleton<IGitWorktreeQueryService, GitWorktreeQueryService>();
        return services;
    }
}
