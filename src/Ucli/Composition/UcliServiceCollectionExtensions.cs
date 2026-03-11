using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.EnvironmentVariables;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Execution.OperationExecute;
using MackySoft.Ucli.Git;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Init;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Logs;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.Ops.Access;
using MackySoft.Ucli.Ops.Mapping;
using MackySoft.Ucli.Ops.Preflight;
using MackySoft.Ucli.Refresh;
using MackySoft.Ucli.Status;
using MackySoft.Ucli.TestProfile;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.TestRun.Results;
using MackySoft.Ucli.TestRun.Service;
using MackySoft.Ucli.TestRun.Service.Mapping;
using MackySoft.Ucli.TestRun.Service.Pipeline;
using MackySoft.Ucli.TestRun.Service.Preflight;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Composition;

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
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IInitService, InitService>();
        services.AddSingleton<IIpcEndpointResolver, IpcEndpointResolver>();
        services.AddSingleton<IUnityIpcTransportClient, UnityIpcTransportClient>();
        services.AddSingleton<IUnityIpcClient, UnityDaemonIpcClient>();
        services.AddSingleton<IUnityIpcClient, UnityOneshotIpcClient>();
        services.AddSingleton<IUnityIpcRequestExecutor, UnityIpcRequestExecutor>();
        services.AddSingleton<IUnityExecutionModeDecisionService, UnityExecutionModeDecisionService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitCommandClient, GitCommandClient>();
        services.AddSingleton<IGitWorktreeListPorcelainParser, GitWorktreeListPorcelainParser>();
        services.AddSingleton<IGitWorktreeQueryService, GitWorktreeQueryService>();
        services.AddSingleton<IOperationCatalogProvider, OperationCatalogProvider>();
        services.AddSingleton<IOperationCatalog, OperationCatalog>();
        services.AddSingleton<IOperationAuthorizationService, OperationAuthorizationService>();
        services.AddSingleton<IRequestStaticValidator, RequestStaticValidator>();
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
        services.AddSingleton<IDaemonProcessTerminationService, DaemonProcessTerminationService>();
        services.AddSingleton<IDaemonArtifactCleaner, DaemonArtifactCleaner>();
        services.AddSingleton<IDaemonSessionCleanupService, DaemonSessionCleanupService>();
        services.AddSingleton<IDaemonExistingSessionGateService, DaemonExistingSessionGateService>();
        services.AddSingleton<IDaemonLaunchSessionService, DaemonLaunchSessionService>();
        services.AddSingleton<IDaemonLaunchCompensationService, DaemonLaunchCompensationService>();
        services.AddSingleton<IDaemonLaunchService, DaemonLaunchService>();
        services.AddSingleton<IDaemonReachabilityClassifier, DaemonReachabilityClassifier>();
        services.AddSingleton<IDaemonStartOperation, DaemonStartOperation>();
        services.AddSingleton<IDaemonStopOperation, DaemonStopOperation>();
        services.AddSingleton<IDaemonStatusOperation, DaemonStatusOperation>();
        services.AddSingleton<IDaemonCommandExecutionContextResolver, DaemonCommandExecutionContextResolver>();
        services.AddSingleton<IDaemonSessionOutputMapper, DaemonSessionOutputMapper>();
        services.AddSingleton<IDaemonDiagnosisOutputMapper, DaemonDiagnosisOutputMapper>();
        services.AddSingleton<IDaemonStartCommandService, DaemonStartCommandService>();
        services.AddSingleton<IDaemonStopCommandService, DaemonStopCommandService>();
        services.AddSingleton<IDaemonStatusCommandService, DaemonStatusCommandService>();
        services.AddSingleton<IDaemonListQueryService, DaemonListQueryService>();
        services.AddSingleton<IDaemonListCommandService, DaemonListCommandService>();
        services.AddSingleton<IDaemonReachabilityProbe, IpcDaemonReachabilityProbe>();
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