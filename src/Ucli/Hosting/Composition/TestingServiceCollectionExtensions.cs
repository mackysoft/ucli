using MackySoft.Ucli.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Features.Testing.Run.Execution;
using MackySoft.Ucli.Features.Testing.Run.Results;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Registers feature services for test profile and test run workflows. </summary>
internal static class TestingServiceCollectionExtensions
{
    /// <summary> Registers testing feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliTestingFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITestProfileInitService, TestProfileInitService>();
        services.AddSingleton<ITestRunMetaStore, TestRunMetaStore>();
        services.AddSingleton<ITestRunArtifactsService, TestRunArtifactsService>();
        services.AddSingleton<IUnityCommandBuilder, UnityCommandBuilder>();
        services.AddSingleton<IUnityTestExecutor, UnityTestExecutor>();
        services.AddSingleton<IDaemonTestRunClient, IpcDaemonTestRunClient>();
        services.AddSingleton<IUnityResultsXmlParser, UnityResultsXmlParser>();
        services.AddSingleton<IUnityResultsArtifactWriter, UnityResultsArtifactWriter>();
        services.AddSingleton<IUnityResultsConverter, UnityResultsConverter>();
        services.AddSingleton<ITestRunProfileLoader, TestRunProfileLoader>();
        services.AddSingleton<ITestRunConfigurationResolver, TestRunConfigurationResolver>();
        services.AddSingleton<ITestRunPreflightService, TestRunPreflightService>();
        services.AddSingleton<ITestRunExecutionPipeline, TestRunExecutionPipeline>();
        services.AddSingleton<ITestRunResultMapper, TestRunResultMapper>();
        services.AddSingleton<ITestRunService, TestRunService>();
        return services;
    }
}