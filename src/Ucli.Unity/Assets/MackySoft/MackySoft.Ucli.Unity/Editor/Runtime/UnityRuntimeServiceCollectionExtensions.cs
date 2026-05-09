using System;
using MackySoft.Ucli.Contracts.Daemon;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Registers runtime services shared by execution and IPC owners. </summary>
    internal static class UnityRuntimeServiceCollectionExtensions
    {
        /// <summary> Registers editor-readiness and main-thread execution services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityRuntimeServices (this IServiceCollection services)
        {
            return AddUnityRuntimeServices(services, DaemonEditorMode.Batchmode);
        }

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

            services.AddSingleton<IUnityEditorReadinessGate>(_ => new UnityEditorReadinessGate(editorMode));
            services.AddSingleton<IUnityMainThreadRequestExecutor>(_ => new UnitySynchronizationContextRequestExecutor());
            return services;
        }
    }
}
