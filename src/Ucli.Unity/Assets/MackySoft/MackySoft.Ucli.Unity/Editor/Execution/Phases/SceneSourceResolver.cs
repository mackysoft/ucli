using System;
using MackySoft.Ucli.Unity.Project;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// Resolves one scene path to the request-appropriate source scene according to a single policy table.
    /// </summary>
    internal static class SceneSourceResolver
    {
        /// <summary>
        /// Selects how one scene source should be acquired.
        /// </summary>
        internal enum Policy
        {
            /// <summary>
            /// Always opens one preview scene from persisted asset contents and closes it after use.
            /// </summary>
            PersistedPreview,

            /// <summary>
            /// Uses one tracked request-local preview scene when available, or opens and tracks one preview scene otherwise.
            /// </summary>
            TrackedTemporaryOrOpen,

            /// <summary>
            /// Uses one tracked request-local preview scene when available, or falls back to one already loaded runtime scene.
            /// </summary>
            TrackedTemporaryOrLoaded,

            /// <summary>
            /// Uses one tracked request-local preview scene when available, or one already loaded runtime scene, or opens one persisted preview scene for one-shot reads otherwise.
            /// </summary>
            TrackedTemporaryOrLoadedOrPersistedPreview,

            /// <summary>
            /// Uses one already loaded runtime scene only.
            /// </summary>
            LoadedOnly,

            /// <summary>
            /// Uses one already loaded runtime scene when available, or opens one persisted preview scene for one-shot reads otherwise.
            /// </summary>
            LoadedOrPersistedPreview,
        }

        /// <summary>
        /// Represents one acquired scene source together with its cleanup contract.
        /// </summary>
        internal readonly struct Lease : IDisposable
        {
            private readonly bool closeAfterUse;

            /// <summary>
            /// Initializes a new instance of the <see cref="Lease" /> struct.
            /// </summary>
            /// <param name="scenePath"> The logical scene asset path that produced <paramref name="scene" />. </param>
            /// <param name="scene"> The acquired scene source. </param>
            /// <param name="closeAfterUse"> <see langword="true" /> when disposing the lease must close one transient preview scene. </param>
            public Lease (
                string scenePath,
                Scene scene,
                bool closeAfterUse)
            {
                ScenePath = scenePath;
                Scene = scene;
                this.closeAfterUse = closeAfterUse;
            }

            /// <summary>
            /// Gets the logical scene asset path associated with the acquired source.
            /// </summary>
            public string ScenePath { get; }

            /// <summary>
            /// Gets the acquired scene source.
            /// </summary>
            public Scene Scene { get; }

            /// <summary>
            /// Releases one transient preview scene when the acquisition policy requires cleanup.
            /// </summary>
            public void Dispose ()
            {
                if (!closeAfterUse)
                {
                    return;
                }

                PersistedPreviewSceneLease.CloseIfNeeded(Scene);
            }
        }

        /// <summary>
        /// Resolves one scene path according to the specified source policy.
        /// </summary>
        /// <param name="scenePath"> The project-relative scene asset path. </param>
        /// <param name="policy"> The acquisition policy that defines which source state is acceptable. </param>
        /// <param name="executionContext"> The current request execution context when the policy needs request-local preview state. </param>
        /// <param name="lease"> The acquired scene source and its cleanup contract when successful. </param>
        /// <param name="errorMessage"> The validation or acquisition error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a scene source matching <paramref name="policy" /> can be acquired; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionContext" /> is required by <paramref name="policy" /> and is <see langword="null" />. </exception>
        public static bool TryAcquire (
            string scenePath,
            Policy policy,
            OperationExecutionContext? executionContext,
            out Lease lease,
            out string errorMessage)
        {
            lease = default;
            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(scenePath, out errorMessage))
            {
                return false;
            }

            // NOTE:
            // Scene-source semantics must stay centralized here so query compilation, selector resolution,
            // and scene-domain operations observe the same persisted/runtime/temporary precedence rules.
            switch (policy)
            {
                case Policy.PersistedPreview:
                    return TryAcquirePersistedPreview(scenePath, out lease, out errorMessage);

                case Policy.TrackedTemporaryOrOpen:
                    if (executionContext == null)
                    {
                        throw new ArgumentNullException(nameof(executionContext));
                    }

                    return TryAcquireTrackedTemporaryOrOpen(scenePath, executionContext, out lease, out errorMessage);

                case Policy.TrackedTemporaryOrLoaded:
                    if (executionContext == null)
                    {
                        throw new ArgumentNullException(nameof(executionContext));
                    }

                    return TryAcquireTrackedTemporaryOrLoaded(scenePath, executionContext, out lease, out errorMessage);

                case Policy.TrackedTemporaryOrLoadedOrPersistedPreview:
                    if (executionContext == null)
                    {
                        throw new ArgumentNullException(nameof(executionContext));
                    }

                    return TryAcquireTrackedTemporaryOrLoadedOrPersistedPreview(scenePath, executionContext, out lease, out errorMessage);

                case Policy.LoadedOnly:
                    return TryAcquireLoadedOnly(scenePath, out lease, out errorMessage);

                case Policy.LoadedOrPersistedPreview:
                    return TryAcquireLoadedOrPersistedPreview(scenePath, out lease, out errorMessage);

                default:
                    errorMessage = "Scene source policy is not supported.";
                    return false;
            }
        }

        private static bool TryAcquirePersistedPreview (
            string scenePath,
            out Lease lease,
            out string errorMessage)
        {
            if (!PersistedPreviewSceneLease.TryOpen(scenePath, out var previewSceneLease, out errorMessage))
            {
                lease = default;
                return false;
            }

            lease = new Lease(previewSceneLease.ScenePath, previewSceneLease.Scene, closeAfterUse: true);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryAcquireTrackedTemporaryOrOpen (
            string scenePath,
            OperationExecutionContext executionContext,
            out Lease lease,
            out string errorMessage)
        {
            lease = default;
            if (!executionContext.TryGetOrOpenTemporaryScene(scenePath, out var scene, out errorMessage))
            {
                return false;
            }

            lease = new Lease(scenePath, scene, closeAfterUse: false);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryAcquireTrackedTemporaryOrLoaded (
            string scenePath,
            OperationExecutionContext executionContext,
            out Lease lease,
            out string errorMessage)
        {
            lease = default;
            if (executionContext.TryGetTemporaryScene(scenePath, out var scene))
            {
                lease = new Lease(scenePath, scene, closeAfterUse: false);
                errorMessage = string.Empty;
                return true;
            }

            if (!SceneOperationUtilities.TryGetLoadedScene(scenePath, out scene, out errorMessage))
            {
                return false;
            }

            lease = new Lease(scenePath, scene, closeAfterUse: false);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryAcquireTrackedTemporaryOrLoadedOrPersistedPreview (
            string scenePath,
            OperationExecutionContext executionContext,
            out Lease lease,
            out string errorMessage)
        {
            lease = default;
            if (executionContext.TryGetTemporaryScene(scenePath, out var temporaryScene))
            {
                lease = new Lease(scenePath, temporaryScene, closeAfterUse: false);
                errorMessage = string.Empty;
                return true;
            }

            if (SceneOperationUtilities.TryGetLoadedScene(scenePath, out var loadedScene, out _))
            {
                lease = new Lease(scenePath, loadedScene, closeAfterUse: false);
                errorMessage = string.Empty;
                return true;
            }

            return TryAcquirePersistedPreview(scenePath, out lease, out errorMessage);
        }

        private static bool TryAcquireLoadedOnly (
            string scenePath,
            out Lease lease,
            out string errorMessage)
        {
            lease = default;
            if (!SceneOperationUtilities.TryGetLoadedScene(scenePath, out var scene, out errorMessage))
            {
                return false;
            }

            lease = new Lease(scenePath, scene, closeAfterUse: false);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryAcquireLoadedOrPersistedPreview (
            string scenePath,
            out Lease lease,
            out string errorMessage)
        {
            lease = default;
            if (SceneOperationUtilities.TryGetLoadedScene(scenePath, out var loadedScene, out _))
            {
                lease = new Lease(scenePath, loadedScene, closeAfterUse: false);
                errorMessage = string.Empty;
                return true;
            }

            return TryAcquirePersistedPreview(scenePath, out lease, out errorMessage);
        }
    }
}
