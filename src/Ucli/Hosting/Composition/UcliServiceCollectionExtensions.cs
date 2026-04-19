using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Features.Daemon.Logs;
using MackySoft.Ucli.Features.Daemon.Runtime;
using MackySoft.Ucli.Features.Daemon.Services;
using MackySoft.Ucli.Features.Daemon.Services.Start;
using MackySoft.Ucli.Features.Daemon.Supervisor;
using MackySoft.Ucli.Features.Init;
using MackySoft.Ucli.Features.OperationCatalog;
using MackySoft.Ucli.Features.OperationCatalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Mapping;
using MackySoft.Ucli.Features.OperationCatalog.Preflight;
using MackySoft.Ucli.Features.Requests.Call;
using MackySoft.Ucli.Features.Requests.Plan;
using MackySoft.Ucli.Features.Requests.Refresh;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Requests.Validate;
using MackySoft.Ucli.Features.Status;
using MackySoft.Ucli.Features.Testing.Profiles;
using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Features.Testing.Run.Execution;
using MackySoft.Ucli.Features.Testing.Run.Results;
using MackySoft.Ucli.Features.Testing.Run.Service;
using MackySoft.Ucli.Features.Testing.Run.Service.Mapping;
using MackySoft.Ucli.Features.Testing.Run.Service.Pipeline;
using MackySoft.Ucli.Features.Testing.Run.Service.Preflight;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.EnvironmentVariables;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Git;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;
using MackySoft.Ucli.UnityIntegration.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Provides DI registration helpers for uCLI application composition roots. </summary>
internal static class UcliServiceCollectionExtensions
{
    /// <summary> Registers core services used across all command families. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliCoreServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<UnityUcliPluginMarkerDiscovery>();
        services.AddSingleton<UnityUcliPluginMarkerValidator>();
        services.AddSingleton<UnityUcliPluginMarkerCacheStore>();
        services.AddSingleton<UnityUcliPluginMarkerCacheCoordinator>();
        services.AddSingleton<IUnityUcliPluginLocator, UnityUcliPluginLocator>();
        services.AddSingleton<IEnvironmentVariableReader, ProcessEnvironmentVariableReader>();
        services.AddSingleton<IProjectPathInputResolver, ProjectPathInputResolver>();
        services.AddSingleton<IUnityProjectResolver>(provider => new UnityProjectResolver(
            provider.GetRequiredService<IProjectPathInputResolver>()));
        services.AddSingleton<IUnityVersionResolver, UnityVersionResolver>();
        services.AddSingleton<IUnityEditorSearchRootProvider, DefaultUnityEditorSearchRootProvider>();
        services.AddSingleton<IUnityEditorPathResolver, UnityEditorPathResolver>();
        services.AddSingleton<IUcliConfigStore, UcliConfigStore>();
        services.AddSingleton<IIndexCatalogReader, FileIndexCatalogReader>();
        services.AddSingleton<IIndexInputFingerprintCalculator, FileSystemIndexInputFingerprintCalculator>();
        services.AddSingleton<IIndexFreshnessEvaluator, IndexFreshnessEvaluator>();
        services.AddSingleton<IAssetLookupStore, FileAssetLookupStore>();
        services.AddSingleton<IAssetLookupSnapshotReader, AssetLookupSnapshotReader>();
        services.AddSingleton<IAssetLookupSourceRefreshService, AssetLookupSourceRefreshService>();
        services.AddSingleton<IAssetSearchLookupAccessService, AssetSearchLookupAccessService>();
        services.AddSingleton<IGuidPathLookupAccessService, GuidPathLookupAccessService>();
        services.AddSingleton<ISceneTreeLiteSourceHashCalculator, SceneTreeLiteSourceHashCalculator>();
        services.AddSingleton<ISceneTreeLiteFreshnessEvaluator, SceneTreeLiteFreshnessEvaluator>();
        services.AddSingleton<ISceneTreeLiteStore, FileSceneTreeLiteStore>();
        services.AddSingleton<ISceneTreeLiteSnapshotReader, SceneTreeLiteSnapshotReader>();
        services.AddSingleton<ISceneTreeLiteSourceRefreshService, SceneTreeLiteSourceRefreshService>();
        services.AddSingleton<ISceneTreeLiteAccessService, SceneTreeLiteAccessService>();
        services.AddSingleton<IPersistedOpsCatalogSnapshotLoader, PersistedOpsCatalogSnapshotLoader>();
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IRequestPreparationService, RequestPreparationService>();
        services.AddSingleton<IInitService, InitService>();
        services.AddSingleton<IIpcEndpointResolver, IpcEndpointResolver>();
        services.AddSingleton<IIpcTransportClient, IpcTransportClient>();
        services.AddSingleton<IUnityIpcTransportClient, UnityIpcTransportClient>();
        services.AddSingleton<IUnityIpcClient, UnityDaemonIpcClient>();
        services.AddSingleton<IUnityIpcClient, UnityOneshotIpcClient>();
        services.AddSingleton<IUnityIpcRequestExecutor, UnityIpcRequestExecutor>();
        services.AddSingleton<IUnityExecutionModeDecisionService, UnityExecutionModeDecisionService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitCommandClient, GitCommandClient>();
        services.AddSingleton<IGitWorktreeListPorcelainParser, GitWorktreeListPorcelainParser>();
        services.AddSingleton<IGitWorktreeQueryService, GitWorktreeQueryService>();
        services.AddSingleton<IOperationCatalogDiscoveryService, OperationCatalogDiscoveryService>();
        services.AddSingleton<IOperationCatalogProvider, OperationCatalogProvider>();
        services.AddSingleton<IOperationCatalog, OperationCatalog>();
        services.AddSingleton<IOperationAuthorizationService, OperationAuthorizationService>();
        services.AddSingleton<IRequestStaticValidator, RequestStaticValidator>();
        services.AddSingleton<IRequestStaticValidationService, RequestStaticValidationService>();
        services.AddSingleton<IRequestInputReader, RequestInputReader>();
        services.AddSingleton<IValidateRequestJsonParser, ValidateRequestJsonParser>();
        services.AddSingleton<IPhaseExecutionPreflightService, PhaseExecutionPreflightService>();
        services.AddSingleton<IOperationExecuteService, OperationExecuteService>();
        return services;
    }

    /// <summary> Registers <c>refresh</c> command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliRefreshServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRefreshService, RefreshService>();
        return services;
    }

    /// <summary> Registers <c>validate</c> command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliValidateServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateMetadataResolver, ValidateMetadataResolver>();
        services.AddSingleton<IValidateService, ValidateService>();
        return services;
    }

    /// <summary> Registers <c>plan</c> command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliPlanServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPlanService, PlanService>();
        return services;
    }

    /// <summary> Registers <c>call</c> command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliCallServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICallDangerousOperationGuard, CallDangerousOperationGuard>();
        services.AddSingleton<ICallUnityExecutionService, CallUnityExecutionService>();
        services.AddSingleton<ICallService, CallService>();
        return services;
    }

    /// <summary> Registers daemon-related services and command orchestration dependencies. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliDaemonServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProjectLifecycleLockProvider, FileSystemProjectLifecycleLockProvider>();
        services.AddSingleton<IDaemonSessionSerializer, DaemonSessionJsonSerializer>();
        services.AddSingleton<IDaemonSessionValidator, DaemonSessionValidator>();
        services.AddSingleton<IDaemonSessionStore, DaemonSessionStore>();
        services.AddSingleton<IDaemonDiagnosisStore, DaemonDiagnosisStore>();
        services.AddSingleton<IDaemonSessionDiagnosisResolver, DaemonSessionDiagnosisResolver>();
        services.AddSingleton<IDaemonSessionTokenGenerator, DaemonSessionTokenGenerator>();
        services.AddSingleton<IDaemonSessionTokenProvider, DaemonSessionTokenProvider>();
        services.AddSingleton<IUnityLogReader, UnityLogReader>();
        services.AddSingleton<IDaemonLogsClient, IpcDaemonLogsClient>();
        services.AddSingleton<ILogsDaemonRequestValidator, LogsDaemonRequestValidator>();
        services.AddSingleton<IDaemonLogsStreamTerminationPolicy, DaemonLogsStreamTerminationPolicy>();
        services.AddSingleton<ILogsDaemonService, LogsDaemonService>();

        services.AddSingleton<IUnityLogsClient, IpcUnityLogsClient>();
        services.AddSingleton<ILogsUnityRequestValidator, LogsUnityRequestValidator>();
        services.AddSingleton<ILogsUnityService, LogsUnityService>();

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

    /// <summary> Registers worktree-local supervisor services for daemon lifecycle ownership. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliSupervisorServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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

    /// <summary> Registers test-run and test-profile command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliTestRunServices (this IServiceCollection services)
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

    /// <summary> Registers status-command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliStatusServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IStatusExecutionContextResolver, StatusExecutionContextResolver>();
        services.AddSingleton<IStatusDaemonObservationService, StatusDaemonObservationService>();
        services.AddSingleton<IStatusService, StatusService>();
        return services;
    }

    /// <summary> Registers <c>ops</c> command services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliOpsServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IOpsCatalogReader, OpsCatalogReader>();
        services.AddSingleton<IOpsCatalogStore, FileOpsCatalogStore>();
        services.AddSingleton<IOpsPreflightService, OpsPreflightService>();
        services.AddSingleton<IOpsCatalogAccessService, OpsCatalogAccessService>();
        services.AddSingleton<OpsReadIndexInfoMapper>();
        services.AddSingleton<IOpsListResultMapper, OpsListResultMapper>();
        services.AddSingleton<IOpsDescribeResultMapper, OpsDescribeResultMapper>();
        services.AddSingleton<IOpsService, OpsService>();
        return services;
    }
}