using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Features;

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

        services.AddUcliDaemonLifecycleServices();
        services.AddUcliDaemonSupervisorServices();
        services.AddUcliDaemonObservabilityServices();
        return services;
    }

    private static IServiceCollection AddUcliDaemonLifecycleServices (this IServiceCollection services)
    {
        services.AddSingleton<IDaemonSessionStore, DaemonSessionStore>();
        services.AddSingleton<IDaemonDiagnosisStore, DaemonDiagnosisStore>();
        services.AddSingleton<IDaemonSessionDiagnosisResolver, DaemonSessionDiagnosisResolver>();
        services.AddSingleton<DaemonProcessIdentityAssessor>();
        services.AddSingleton<IDaemonProcessIdentityAssessor>(provider => provider.GetRequiredService<DaemonProcessIdentityAssessor>());
        services.AddSingleton<IDaemonProcessTerminationService, DaemonProcessTerminationService>();
        services.AddSingleton<IDaemonReachabilityClassifier, DaemonReachabilityClassifier>();
        services.AddSingleton<UnityBatchmodeProcessLauncher>();
        services.AddSingleton<IUnityDaemonProcessLauncher>(provider => provider.GetRequiredService<UnityBatchmodeProcessLauncher>());
        services.AddSingleton<IUnityBatchmodeProcessLauncher>(provider => provider.GetRequiredService<UnityBatchmodeProcessLauncher>());
        services.AddSingleton<IpcDaemonPingClient>();
        services.AddSingleton<IDaemonPingClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
        services.AddSingleton<IDaemonPingInfoClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
        services.AddSingleton<IDaemonStartupReadinessProbe, DaemonStartupReadinessProbe>();
        services.AddSingleton<IDaemonShutdownClient, DaemonShutdownClient>();
        services.AddSingleton<IDaemonArtifactCleaner, DaemonArtifactCleaner>();
        services.AddSingleton<IDaemonCleanupReachabilityProbe, DaemonCleanupReachabilityProbe>();
        services.AddSingleton<IDaemonLaunchSessionService, DaemonLaunchSessionService>();
        services.AddSingleton<IDaemonLaunchService, DaemonLaunchService>();
        services.AddSingleton<IDaemonReachabilityProbe, IpcDaemonReachabilityProbe>();
        return services;
    }

    private static IServiceCollection AddUcliDaemonObservabilityServices (this IServiceCollection services)
    {
        services.AddSingleton<IUnityLogReader, UnityLogReader>();
        services.AddSingleton<IDaemonLogsClient, IpcDaemonLogsClient>();
        services.AddSingleton<IUnityLogsClient, IpcUnityLogsClient>();
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
        services.AddSingleton<IDaemonProjectLifecycleGateway, SupervisorProjectGateway>();
        services.AddSingleton<SupervisorHost>();
        return services;
    }
}
