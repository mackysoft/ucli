using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Preflight;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Preflight;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Application;

/// <summary> Registers application-core services that do not own host or adapter resources. </summary>
public static class UcliApplicationServiceCollectionExtensions
{
    /// <summary> Registers use cases and application-internal policies for uCLI. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliApplicationServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUcliApplicationSharedServices();
        services.AddUcliApplicationRequestServices();
        services.AddUcliApplicationOperationCatalogServices();
        services.AddUcliApplicationDaemonServices();
        services.AddUcliApplicationInitServices();
        services.AddUcliApplicationStatusServices();
        services.AddUcliApplicationTestingServices();
        return services;
    }

    private static IServiceCollection AddUcliApplicationSharedServices (this IServiceCollection services)
    {
        services.AddSingleton<IProjectContextResolver, ProjectContextResolver>();
        services.AddSingleton<IUnityExecutionModeDecisionService, UnityExecutionModeDecisionService>();
        return services;
    }

    private static IServiceCollection AddUcliApplicationRequestServices (this IServiceCollection services)
    {
        services.AddSingleton<IRequestPreparationService, RequestPreparationService>();
        services.AddSingleton<IRequestIdFactory, GuidRequestIdFactory>();
        services.AddSingleton<IRequestStaticValidationPreflightService, RequestStaticValidationPreflightService>();
        services.AddSingleton<IValidateRequestJsonParser, ValidateRequestJsonParser>();

        services.AddSingleton<IPhaseExecutionPreflightService, PhaseExecutionPreflightService>();
        services.AddSingleton<IOperationExecuteService, OperationExecuteService>();

        services.AddSingleton<IOperationCatalogDiscoveryService, OperationCatalogDiscoveryService>();
        services.AddSingleton<IOperationCatalogProvider, OperationCatalogProvider>();
        services.AddSingleton<IOperationCatalog, OperationCatalog>();
        services.AddSingleton<IOperationAuthorizationService, OperationAuthorizationService>();
        services.AddSingleton<IReadIndexValidationCatalogResolver, ReadIndexValidationCatalogResolver>();
        services.AddSingleton<IRequestStaticValidator, RequestStaticValidator>();
        services.AddSingleton<IRequestStaticValidationService, RequestStaticValidationService>();

        services.AddSingleton<IPlanCommandPreflightService, PlanCommandPreflightService>();
        services.AddSingleton<IPlanService, PlanService>();

        services.AddSingleton<ICallDangerousOperationGuard, CallDangerousOperationGuard>();
        services.AddSingleton<ICallUnityExecutionService, CallUnityExecutionService>();
        services.AddSingleton<ICallCommandPreflightService, CallCommandPreflightService>();
        services.AddSingleton<ICallService, CallService>();

        services.AddSingleton<IQueryService, QueryService>();
        services.AddSingleton<IRefreshService, RefreshService>();
        services.AddSingleton<IResolveService, ResolveService>();
        services.AddSingleton<IValidateService, ValidateService>();
        return services;
    }

    private static IServiceCollection AddUcliApplicationOperationCatalogServices (this IServiceCollection services)
    {
        services.AddSingleton<IOpsPreflightService, OpsPreflightService>();
        services.AddSingleton<IOpsCatalogAccessService, OpsCatalogAccessService>();
        services.AddSingleton<OpsReadIndexInfoMapper>();
        services.AddSingleton<IOpsListResultMapper, OpsListResultMapper>();
        services.AddSingleton<IOpsDescribeResultMapper, OpsDescribeResultMapper>();
        services.AddSingleton<IOpsService, OpsService>();
        return services;
    }

    private static IServiceCollection AddUcliApplicationDaemonServices (this IServiceCollection services)
    {
        services.AddSingleton<IDaemonSessionSerializer, DaemonSessionJsonSerializer>();
        services.AddSingleton<IDaemonSessionValidator, DaemonSessionValidator>();
        services.AddSingleton<IDaemonSessionTokenGenerator, DaemonSessionTokenGenerator>();
        services.AddSingleton<IDaemonSessionTokenProvider, DaemonSessionTokenProvider>();

        services.AddSingleton<IDaemonSessionCleanupService, DaemonSessionCleanupService>();
        services.AddSingleton<IDaemonExistingSessionGateService, DaemonExistingSessionGateService>();
        services.AddSingleton<IDaemonLaunchCompensationService, DaemonLaunchCompensationService>();
        services.AddSingleton<IDaemonStartOperation, DaemonStartOperation>();
        services.AddSingleton<IDaemonStopOperation, DaemonStopOperation>();
        services.AddSingleton<IDaemonCleanupOperation, DaemonCleanupOperation>();
        services.AddSingleton<IDaemonStatusOperation, DaemonStatusOperation>();
        services.AddSingleton<IDaemonInvalidSessionCleanupSafetyEvaluator, DaemonInvalidSessionCleanupSafetyEvaluator>();

        services.AddSingleton<ILogsDaemonRequestValidator, LogsDaemonRequestValidator>();
        services.AddSingleton<IDaemonLogsStreamTerminationPolicy, DaemonLogsStreamTerminationPolicy>();
        services.AddSingleton<ILogsDaemonService, LogsDaemonService>();
        services.AddSingleton<ILogsUnityRequestValidator, LogsUnityRequestValidator>();
        services.AddSingleton<ILogsUnityService, LogsUnityService>();

        services.AddSingleton<IDaemonCommandExecutionContextResolver, DaemonCommandExecutionContextResolver>();
        services.AddSingleton<IDaemonSessionOutputMapper, DaemonSessionOutputMapper>();
        services.AddSingleton<IDaemonDiagnosisOutputMapper, DaemonDiagnosisOutputMapper>();
        services.AddSingleton<IDaemonCleanupService, DaemonCleanupService>();
        services.AddSingleton<IDaemonStartService, DaemonStartService>();
        services.AddSingleton<IDaemonStatusService, DaemonStatusService>();
        services.AddSingleton<IDaemonStopService, DaemonStopService>();
        services.AddSingleton<IDaemonListQueryService, DaemonListQueryService>();
        services.AddSingleton<IDaemonListService, DaemonListService>();
        return services;
    }

    private static IServiceCollection AddUcliApplicationStatusServices (this IServiceCollection services)
    {
        services.AddSingleton<IStatusExecutionContextResolver, StatusExecutionContextResolver>();
        services.AddSingleton<IStatusDaemonObservationService, StatusDaemonObservationService>();
        services.AddSingleton<IStatusService, StatusService>();
        return services;
    }

    private static IServiceCollection AddUcliApplicationInitServices (this IServiceCollection services)
    {
        services.AddSingleton<IInitService, InitService>();
        return services;
    }

    private static IServiceCollection AddUcliApplicationTestingServices (this IServiceCollection services)
    {
        services.AddSingleton<IUnityResultsConverter, UnityResultsConverter>();
        services.AddSingleton<ITestProfileInitService, TestProfileInitService>();
        services.AddSingleton<ITestRunProfileLoader, TestRunProfileLoader>();
        services.AddSingleton<ITestRunConfigurationResolver, TestRunConfigurationResolver>();
        services.AddSingleton<ITestRunPreflightService, TestRunPreflightService>();
        services.AddSingleton<ITestRunExecutionPipeline, TestRunExecutionPipeline>();
        services.AddSingleton<ITestRunResultMapper, TestRunResultMapper>();
        services.AddSingleton<ITestRunService, TestRunService>();
        return services;
    }
}
