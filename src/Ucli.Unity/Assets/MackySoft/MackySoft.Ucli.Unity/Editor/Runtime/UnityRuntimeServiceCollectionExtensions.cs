using System;
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
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IUnityEditorReadinessGate, UnityEditorReadinessGate>();
            services.AddSingleton<IUnityMainThreadRequestExecutor>(new UnitySynchronizationContextRequestExecutor());
            return services;
        }
    }
}
