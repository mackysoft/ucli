using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Project;

#nullable enable

namespace MackySoft.Ucli.Unity.SceneInspection
{
    /// <summary> Acquires read-only scene sources without depending on request execution state. </summary>
    internal static class SceneReadSourceResolver
    {
        /// <summary> Opens one persisted preview scene and returns a lease that closes it after use. </summary>
        /// <param name="scenePath"> The project-relative scene asset path. </param>
        /// <param name="lease"> The acquired scene source and its cleanup contract when successful. </param>
        /// <param name="errorMessage"> The validation or acquisition error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a persisted preview scene can be acquired; otherwise <see langword="false" />. </returns>
        public static bool TryAcquirePersistedPreview (
            string scenePath,
            out SceneSourceLease lease,
            out string errorMessage)
        {
            lease = default;
            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            return TryOpenPersistedPreview(scenePath, out lease, out errorMessage);
        }

        /// <summary> Uses one already loaded runtime scene. </summary>
        /// <param name="scenePath"> The project-relative scene asset path. </param>
        /// <param name="lease"> The acquired scene source and its cleanup contract when successful. </param>
        /// <param name="errorMessage"> The validation or acquisition error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the scene is already loaded; otherwise <see langword="false" />. </returns>
        public static bool TryAcquireLoadedOnly (
            string scenePath,
            out SceneSourceLease lease,
            out string errorMessage)
        {
            lease = default;
            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            if (!SceneAssetSourceUtilities.TryGetLoadedScene(scenePath, out var scene, out errorMessage))
            {
                return false;
            }

            lease = new SceneSourceLease(
                scenePath,
                scene,
                SceneTreeSourceStateKind.LoadedScene,
                closeAfterUse: false);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Uses one already loaded runtime scene, or opens one persisted preview scene for one-shot reads otherwise. </summary>
        /// <param name="scenePath"> The project-relative scene asset path. </param>
        /// <param name="lease"> The acquired scene source and its cleanup contract when successful. </param>
        /// <param name="errorMessage"> The validation or acquisition error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a loaded or persisted-preview source can be acquired; otherwise <see langword="false" />. </returns>
        public static bool TryAcquireLoadedOrPersistedPreview (
            string scenePath,
            out SceneSourceLease lease,
            out string errorMessage)
        {
            lease = default;
            if (!SceneAssetSourceUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
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

            return TryOpenPersistedPreview(scenePath, out lease, out errorMessage);
        }

        private static bool TryOpenPersistedPreview (
            string scenePath,
            out SceneSourceLease lease,
            out string errorMessage)
        {
            if (!PersistedPreviewSceneLease.TryOpen(scenePath, out var previewSceneLease, out errorMessage))
            {
                lease = default;
                return false;
            }

            lease = new SceneSourceLease(
                previewSceneLease.ScenePath,
                previewSceneLease.Scene,
                SceneTreeSourceStateKind.PersistedPreview,
                closeAfterUse: true);
            errorMessage = string.Empty;
            return true;
        }
    }
}
