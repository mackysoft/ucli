using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.EditorInstance;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.GuiEditor;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Inventory;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Infrastructure.Storage;
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
        services.AddSingleton<IDaemonIpcRequestSender, DaemonIpcRequestSender>();
        services.AddUcliDaemonSupervisorServices();
        services.AddUcliDaemonObservabilityServices();
        return services;
    }

    private static IServiceCollection AddUcliDaemonLifecycleServices (this IServiceCollection services)
    {
        services.AddSingleton<IDaemonSessionStore, DaemonSessionStore>();
        services.AddSingleton<IDaemonDiagnosisStore, DaemonDiagnosisStore>();
        services.AddSingleton<IDaemonLaunchAttemptStore, DaemonLaunchAttemptStore>();
        services.AddSingleton<IDaemonLifecycleStore, DaemonLifecycleStore>();
        services.AddSingleton<IDaemonSessionDiagnosisResolver, DaemonSessionDiagnosisResolver>();
        services.AddSingleton<DaemonProcessIdentityAssessor>();
        services.AddSingleton<IDaemonProcessIdentityAssessor>(provider => provider.GetRequiredService<DaemonProcessIdentityAssessor>());
        services.AddSingleton<IDaemonProcessTerminationService, DaemonProcessTerminationService>();
        services.AddSingleton<IDaemonReachabilityClassifier, DaemonReachabilityClassifier>();
        services.AddSingleton<IUnityEditorInstanceMarkerReader, UnityEditorInstanceMarkerReader>();
        services.AddSingleton<IUnityGuiEditorProcessInspector, UnityGuiEditorProcessInspector>();
        services.AddSingleton<IUnityGuiEditorProcessProbe, UnityGuiEditorProcessProbe>();
        services.AddSingleton<UnityBatchmodeProcessLauncher>();
        services.AddSingleton<IUnityDaemonProcessLauncher>(provider => provider.GetRequiredService<UnityBatchmodeProcessLauncher>());
        services.AddSingleton<IUnityBatchmodeProcessLauncher>(provider => provider.GetRequiredService<UnityBatchmodeProcessLauncher>());
        services.AddSingleton<UnityGuiEditorProcessLauncher>();
        services.AddSingleton<IUnityGuiEditorProcessLauncher>(provider => provider.GetRequiredService<UnityGuiEditorProcessLauncher>());
        services.AddSingleton<IpcDaemonPingClient>();
        services.AddSingleton<IDaemonPingClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
        services.AddSingleton<IDaemonPingInfoClient>(provider => provider.GetRequiredService<IpcDaemonPingClient>());
        services.AddSingleton<IDaemonStartupReadinessProbe, DaemonStartupReadinessProbe>();
        services.AddSingleton<IDaemonGuiStartupObserver, DaemonGuiStartupObserver>();
        services.AddSingleton<IGuiSupervisorManifestStore, GuiSupervisorManifestStore>();
        services.AddSingleton<IDaemonGuiRebootstrapClient, DaemonGuiRebootstrapClient>();
        services.AddSingleton<IDaemonShutdownClient, DaemonShutdownClient>();
        services.AddSingleton<IDaemonArtifactCleaner, DaemonArtifactCleaner>();
        services.AddSingleton<IDaemonCleanupReachabilityProbe, DaemonCleanupReachabilityProbe>();
        services.AddSingleton<IWorktreeProjectPathResolver, WorktreeProjectPathResolver>();
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
        services.AddSingleton<IUnityConsoleClearClient, IpcUnityConsoleClearClient>();
        return services;
    }

    private static IServiceCollection AddUcliDaemonSupervisorServices (this IServiceCollection services)
    {
        services.AddSingleton<SupervisorActivityTracker>();
        services.AddSingleton<SupervisorRuntimeLogger>();
        services.AddSingleton(serviceProvider => new SupervisorManifestStore(
            serviceProvider.GetRequiredService<TimeProvider>(),
            static (path, cancellationToken) => FileUtilities.ReadAllBytesOrNullAsync(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllBytesAtomicallyAsync(path, contents, cancellationToken),
            static path => FileUtilities.DeleteIfExists(path)));
        services.AddSingleton<SupervisorEndpointResolver>();
        services.AddSingleton<SupervisorBootstrapLockProvider>();
        services.AddSingleton<SupervisorDiagnosisWriter>();
        services.AddSingleton<SupervisorExitHandler>();
        services.AddSingleton<SupervisorStabilityVerifier>();
        services.AddSingleton<SupervisorProjectCoordinator>();
        services.AddSingleton<SupervisorRequestDispatcher>();
        services.AddSingleton<SupervisorTransportServer>();
        services.AddSingleton<SupervisorLaunchCommandResolver>();
        services.AddSingleton<LaunchdSupervisorProcessManager>();
        services.AddSingleton<SystemdRunSupervisorProcessLauncher>();
        services.AddSingleton<IDetachedProcessStarter, DetachedProcessStarter>();
        services.AddSingleton<WindowsDetachedSupervisorProcessLauncher>();
        services.AddSingleton<SupervisorProcessManager>();
        services.AddSingleton<ISupervisorProcessManager>(provider => provider.GetRequiredService<SupervisorProcessManager>());
        services.AddSingleton<SupervisorBootstrapper>();
        services.AddSingleton<SupervisorClient>();
        services.AddSingleton<IDaemonProjectLifecycleGateway, SupervisorProjectGateway>();
        services.AddSingleton<SupervisorHost>();
        return services;
    }
}
