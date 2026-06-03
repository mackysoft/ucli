using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.SceneInspection;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// Resolves one scene path against request-local temporary scene state before falling back to read-only sources.
    /// </summary>
    internal static class SceneSourceResolver
    {
        /// <summary>
        /// Acquires a scene source by observing request-local temporary state first, then a loaded scene, then one persisted preview scene.
        /// </summary>
        /// <param name="scenePath"> The project-relative scene asset path. </param>
        /// <param name="executionContext"> The current request execution context used to observe earlier plan-time temporary state. </param>
        /// <param name="lease"> The acquired scene source and its cleanup contract when successful. </param>
        /// <param name="errorMessage"> The validation or acquisition error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a source can be acquired; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionContext" /> is <see langword="null" />. </exception>
        public static bool TryAcquireTrackedTemporaryOrLoadedOrPersistedPreview (
            string scenePath,
            OperationExecutionContext executionContext,
            out SceneSourceLease lease,
            out string errorMessage)
        {
            lease = default;
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            if (executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene))
            {
                lease = new SceneSourceLease(
                    scenePath,
                    temporaryScene,
                    SceneTreeSourceStateKind.TemporaryScene,
                    closeAfterUse: false);
                errorMessage = string.Empty;
                return true;
            }

            if (SceneAssetSourceUtilities.TryGetLoadedScene(scenePath, out var loadedScene, out _))
            {
                lease = new SceneSourceLease(
                    scenePath,
                    loadedScene,
                    SceneTreeSourceStateKind.LoadedScene,
                    closeAfterUse: false);
                errorMessage = string.Empty;
                return true;
            }

            return SceneReadSourceResolver.TryAcquirePersistedPreview(scenePath, out lease, out errorMessage);
        }
    }
}
