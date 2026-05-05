using MackySoft.Ucli.Application.Features.Testing.Profiles.Ports;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Features.Testing.Profiles.Adapters;
using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Execution;
using MackySoft.Ucli.Features.Testing.Run.Results;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

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

        services.AddSingleton<ITestProfileTemplateStore, FileTestProfileTemplateStore>();
        services.AddSingleton<ITestRunMetaStore, TestRunMetaStore>();
        services.AddSingleton<ITestRunArtifactsService, TestRunArtifactsService>();
        services.AddSingleton<IDaemonTestRunClient, IpcDaemonTestRunClient>();
        services.AddSingleton<ITestRunProfileJsonReader, FileTestRunProfileJsonReader>();
        services.AddSingleton<ITestRunPathExistenceProbe, FileTestRunPathExistenceProbe>();
        services.AddSingleton<IUnityCommandBuilder, UnityCommandBuilder>();
        services.AddSingleton<IUnityTestExecutor, UnityTestExecutor>();
        services.AddSingleton<IUnityResultsXmlParser, UnityResultsXmlParser>();
        services.AddSingleton<IUnityResultsArtifactWriter, UnityResultsArtifactWriter>();
        return services;
    }
}
