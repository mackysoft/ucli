using System;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Registers index readers and snapshot builders consumed by IPC methods. </summary>
    internal static class UnityIndexServiceCollectionExtensions
    {
        /// <summary> Registers shared index services. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <returns> The updated service collection. </returns>
        public static IServiceCollection AddUnityIndexServices (this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IAssetLookupSnapshotBuilder, AssetLookupSnapshotBuilder>();
            services.AddSingleton<ISceneTreeLiteSnapshotBuilder, SceneTreeLiteSnapshotBuilder>();
            return services;
        }
    }
}
