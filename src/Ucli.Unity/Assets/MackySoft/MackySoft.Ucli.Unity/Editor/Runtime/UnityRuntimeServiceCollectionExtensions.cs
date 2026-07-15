using System;
using System.Threading;
using MackySoft.Ucli.Contracts.Daemon;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Registers runtime services shared by execution and IPC owners. </summary>
    internal static class UnityRuntimeServiceCollectionExtensions
    {
        /// <summary> Registers editor-readiness and main-thread execution services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="editorMode"> The daemon Editor mode reported by lifecycle snapshots. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityRuntimeServices (
            this IServiceCollection services,
            DaemonEditorMode editorMode)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var mainThreadSynchronizationContext = UnityMainThreadGuard.CaptureSynchronizationContext(
                nameof(AddUnityRuntimeServices));
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            services.AddSingleton<IMonotonicClock, StopwatchMonotonicClock>();
            services.AddSingleton(_ => new UnitySynchronizationContextRequestExecutor(
                mainThreadSynchronizationContext,
                mainThreadId,
                UnitySynchronizationContextRequestExecutor.DefaultMaxPendingInvocations));
            services.AddSingleton<IUnityMainThreadRequestExecutor>(serviceProvider =>
                serviceProvider.GetRequiredService<UnitySynchronizationContextRequestExecutor>());
            services.AddSingleton<IUnityMutationExecutionState>(serviceProvider =>
                serviceProvider.GetRequiredService<UnitySynchronizationContextRequestExecutor>());
            services.AddSingleton<IUnityMutationLaneControl>(serviceProvider =>
                serviceProvider.GetRequiredService<UnitySynchronizationContextRequestExecutor>());
            services.AddSingleton<IUnityMutationRequestExecutionStartSource>(serviceProvider =>
                serviceProvider.GetRequiredService<UnitySynchronizationContextRequestExecutor>());
            services.AddSingleton(_ => new UnityControlPlaneRequestExecutor(
                    mainThreadSynchronizationContext,
                    mainThreadId,
                    UnityControlPlaneRequestExecutor.DefaultMaxConcurrentInvocations));
            services.AddSingleton<IUnityControlPlaneRequestExecutor>(serviceProvider =>
                serviceProvider.GetRequiredService<UnityControlPlaneRequestExecutor>());
            services.AddSingleton<IUnityControlPlaneRequestLifetime>(serviceProvider =>
                serviceProvider.GetRequiredService<UnityControlPlaneRequestExecutor>());
            services.AddSingleton(serviceProvider => new UnityEditorReadinessGate(
                editorMode,
                serviceProvider.GetRequiredService<IUnityMutationExecutionState>()));
            services.AddSingleton<IUnityEditorReadinessGate>(serviceProvider =>
                serviceProvider.GetRequiredService<UnityEditorReadinessGate>());
            services.AddSingleton<IUnityEditorAvailabilityObservationSource>(serviceProvider =>
                serviceProvider.GetRequiredService<UnityEditorReadinessGate>());
            return services;
        }
    }
}
