using MackySoft.Ucli.Features.Daemon.Logs;
using MackySoft.Ucli.Features.Daemon.Runtime;
using MackySoft.Ucli.Features.Daemon.Services;
using MackySoft.Ucli.Features.Daemon.Services.Start;
using MackySoft.Ucli.Features.Daemon.Supervisor;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Registers feature services for daemon runtime, logs, and supervisor ownership. </summary>
internal static class DaemonServiceCollectionExtensions
{
    /// <summary> Registers daemon feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliDaemonFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUcliDaemonRuntimeServices();
        services.AddUcliDaemonLogServices();
        services.AddUcliDaemonSupervisorServices();
        return services;
    }

    private static IServiceCollection AddUcliDaemonRuntimeServices (this IServiceCollection services)
    {
        services.AddSingleton<IDaemonSessionSerializer, DaemonSessionJsonSerializer>();
        services.AddSingleton<IDaemonSessionValidator, DaemonSessionValidator>();
        services.AddSingleton<IDaemonSessionStore, DaemonSessionStore>();
        services.AddSingleton<IDaemonDiagnosisStore, DaemonDiagnosisStore>();
        services.AddSingleton<IDaemonSessionDiagnosisResolver, DaemonSessionDiagnosisResolver>();
        services.AddSingleton<IDaemonSessionTokenGenerator, DaemonSessionTokenGenerator>();
        services.AddSingleton<IDaemonSessionTokenProvider, DaemonSessionTokenProvider>();
        services.AddSingleton<UnityBatchmodeProcessLauncher>();
        services.AddSingleton<IUnityDaemonProcessLauncher>(provider => provider.GetRequiredService<UnityBatchmodeProcessLauncher>());
        services.AddSingleton<IUnityBatchmodeProcessLauncher>(provider => provider.GetRequiredService<UnityBatchmodeProcessLauncher>());
        services.AddSingleton<IpcDaemonPingClient>();
        services.AddSingleton<IDaemonPingClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
        services.AddSingleton<IDaemonPingInfoClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
        services.AddSingleton<IDaemonStartupReadinessProbe, DaemonStartupReadinessProbe>();
        services.AddSingleton<IDaemonShutdownClient, DaemonShutdownClient>();
        services.AddSingleton<IDaemonProcessIdentityAssessor, DaemonProcessIdentityAssessor>();
        services.AddSingleton<IDaemonProcessTerminationService, DaemonProcessTerminationService>();
        services.AddSingleton<IDaemonArtifactCleaner, DaemonArtifactCleaner>();
        services.AddSingleton<IDaemonCleanupReachabilityProbe, DaemonCleanupReachabilityProbe>();
        services.AddSingleton<IDaemonSessionCleanupService, DaemonSessionCleanupService>();
        services.AddSingleton<IDaemonExistingSessionGateService, DaemonExistingSessionGateService>();
        services.AddSingleton<IDaemonLaunchSessionService, DaemonLaunchSessionService>();
        services.AddSingleton<IDaemonLaunchCompensationService, DaemonLaunchCompensationService>();
        services.AddSingleton<IDaemonLaunchService, DaemonLaunchService>();
        services.AddSingleton<IDaemonReachabilityClassifier, DaemonReachabilityClassifier>();
        services.AddSingleton<IDaemonStartOperation, DaemonStartOperation>();
        services.AddSingleton<IDaemonStopOperation, DaemonStopOperation>();
        services.AddSingleton<IDaemonCleanupOperation, DaemonCleanupOperation>();
        services.AddSingleton<IDaemonStatusOperation, DaemonStatusOperation>();
        services.AddSingleton<IDaemonInvalidSessionCleanupSafetyEvaluator, DaemonInvalidSessionCleanupSafetyEvaluator>();
        services.AddSingleton<IDaemonCommandExecutionContextResolver, DaemonCommandExecutionContextResolver>();
        services.AddSingleton<IDaemonSessionOutputMapper, DaemonSessionOutputMapper>();
        services.AddSingleton<IDaemonDiagnosisOutputMapper, DaemonDiagnosisOutputMapper>();
        services.AddSingleton<IDaemonStartService, DaemonStartService>();
        services.AddSingleton<IDaemonStopService, DaemonStopService>();
        services.AddSingleton<IDaemonCleanupService, DaemonCleanupService>();
        services.AddSingleton<IDaemonStatusService, DaemonStatusService>();
        services.AddSingleton<IDaemonListQueryService, DaemonListQueryService>();
        services.AddSingleton<IDaemonListService, DaemonListService>();
        services.AddSingleton<IDaemonReachabilityProbe, IpcDaemonReachabilityProbe>();
        return services;
    }

    private static IServiceCollection AddUcliDaemonLogServices (this IServiceCollection services)
    {
        services.AddSingleton<IUnityLogReader, UnityLogReader>();
        services.AddSingleton<IDaemonLogsClient, IpcDaemonLogsClient>();
        services.AddSingleton<ILogsDaemonRequestValidator, LogsDaemonRequestValidator>();
        services.AddSingleton<IDaemonLogsStreamTerminationPolicy, DaemonLogsStreamTerminationPolicy>();
        services.AddSingleton<ILogsDaemonService, LogsDaemonService>();
        services.AddSingleton<IUnityLogsClient, IpcUnityLogsClient>();
        services.AddSingleton<ILogsUnityRequestValidator, LogsUnityRequestValidator>();
        services.AddSingleton<ILogsUnityService, LogsUnityService>();
        return services;
    }

    private static IServiceCollection AddUcliDaemonSupervisorServices (this IServiceCollection services)
    {
        services.AddSingleton<SupervisorActivityTracker>();
        services.AddSingleton<SupervisorRuntimeLogger>();
        services.AddSingleton<SupervisorManifestStore>();
        services.AddSingleton<SupervisorEndpointResolver>();
        services.AddSingleton<SupervisorBootstrapLockProvider>();
        services.AddSingleton<SupervisorDiagnosisWriter>();
        services.AddSingleton<SupervisorExitHandler>();
        services.AddSingleton<SupervisorStabilityVerifier>();
        services.AddSingleton<SupervisorProjectCoordinator>();
        services.AddSingleton<SupervisorRequestDispatcher>();
        services.AddSingleton<SupervisorTransportServer>();
        services.AddSingleton<SupervisorLaunchCommandResolver>();
        services.AddSingleton<SupervisorExternalProcessRunner>();
        services.AddSingleton<LaunchdSupervisorProcessLauncher>();
        services.AddSingleton<SystemdRunSupervisorProcessLauncher>();
        services.AddSingleton<WindowsDetachedSupervisorProcessLauncher>();
        services.AddSingleton<SupervisorProcessLauncher>();
        services.AddSingleton<ISupervisorProcessLauncher>(provider => provider.GetRequiredService<SupervisorProcessLauncher>());
        services.AddSingleton<SupervisorBootstrapper>();
        services.AddSingleton<SupervisorClient>();
        services.AddSingleton<SupervisorHost>();
        return services;
    }
}