using System;
using MackySoft.Ucli.Unity.Execution.CsEval;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Execution.RequestIdempotency;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Execution
{
    /// <summary> Registers execution services shared by execute and ops-read IPC methods. </summary>
    internal static class UnityExecutionServiceCollectionExtensions
    {
        /// <summary> Registers shared execution services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityExecutionServices (this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddUnityOperationServices();
            services.AddSingleton(static serviceProvider => UcliOperationCatalogSnapshotBuilder.Build(serviceProvider));
            services.AddSingleton<IPhaseOperationRegistry>(serviceProvider => new InMemoryPhaseOperationRegistry(
                serviceProvider.GetRequiredService<UcliOperationCatalogSnapshot>().Registrations));
            services.AddSingleton<OperationPlanStepRunner>();
            services.AddSingleton<ExecuteRequestCompiler>();
            services.AddSingleton<IOperationPlanPassExecutor, OperationPlanPassExecutor>();
            services.AddSingleton<IOperationCallPassExecutor, OperationCallPassExecutor>();
            services.AddSingleton<IPlanTokenEnvironment, DefaultPlanTokenEnvironment>();
            services.AddSingleton<IPlanTokenCoordinator, PlanTokenCoordinator>();
            services.AddSingleton<IDangerousOperationCallAuthorizer, DangerousOperationCallAuthorizer>();
            services.AddSingleton<IOperationPhaseExecutor, OperationPhaseExecutor>();
            services.AddSingleton<IExecuteRequestNormalizer, ExecuteRequestNormalizer>();
            services.AddSingleton<IExecuteRequestIdempotencyStore>(serviceProvider => new InMemoryExecuteRequestIdempotencyStore(
                ExecuteRequestIdempotencyCoordinator.DefaultCacheTtl,
                ExecuteRequestIdempotencyCoordinator.DefaultMaxEntries,
                serviceProvider.GetRequiredService<IMonotonicClock>()));
            services.AddSingleton<IExecuteRequestIdempotencyCoordinator, ExecuteRequestIdempotencyCoordinator>();
            services.AddSingleton<IExecuteRequestDispatcher, ExecuteRequestDispatcher>();
            return services;
        }

        /// <summary> Registers built-in operation dependencies used by operation discovery. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <returns> The updated service collection. </returns>
        internal static IServiceCollection AddUnityOperationServices (this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<CsEvalReferenceResolver>();
            services.AddSingleton<CsEvalEntryPointSymbolValidator>();
            services.AddSingleton<CsEvalSourcePreparer>();
            services.AddSingleton<CsEvalCompilationService>();
            services.AddSingleton<CsEvalEntryPointReflectionResolver>();
            services.AddSingleton<CsEvalReturnValueSerializer>();
            services.AddSingleton<CsEvalOperation>();
            return services;
        }
    }
}
