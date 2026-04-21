using System;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
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

            services.AddSingleton(static _ => UcliOperationCatalogSnapshotBuilder.Build());
            services.AddSingleton<IExecuteRequestDispatcher>(serviceProvider => CreateExecuteRequestDispatcher(
                serviceProvider.GetRequiredService<UcliOperationCatalogSnapshot>(),
                serviceProvider.GetRequiredService<IUnityEditorReadinessGate>(),
                serviceProvider.GetRequiredService<IUnityMainThreadRequestExecutor>()));
            return services;
        }

        private static IExecuteRequestDispatcher CreateExecuteRequestDispatcher (
            UcliOperationCatalogSnapshot snapshot,
            IUnityEditorReadinessGate readinessGate,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor)
        {
            var normalizer = new ExecuteRequestNormalizer();
            var operationRegistry = new InMemoryPhaseOperationRegistry(snapshot.Registrations);
            var phaseExecutor = new OperationPhaseExecutor(operationRegistry);
            return new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate, mainThreadRequestExecutor);
        }
    }
}
