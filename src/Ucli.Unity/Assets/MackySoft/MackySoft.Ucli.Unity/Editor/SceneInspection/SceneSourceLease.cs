using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Project;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.SceneInspection
{
    /// <summary> Represents one acquired scene source together with its cleanup contract. </summary>
    internal readonly struct SceneSourceLease : IDisposable
    {
        private readonly bool closeAfterUse;

        /// <summary> Initializes a new instance of the <see cref="SceneSourceLease" /> struct. </summary>
        /// <param name="scenePath"> The logical scene asset path that produced <paramref name="scene" />. </param>
        /// <param name="scene"> The acquired scene source. </param>
        /// <param name="sourceKind"> The source kind that produced <paramref name="scene" />. </param>
        /// <param name="closeAfterUse"> <see langword="true" /> when disposing the lease must close one transient preview scene. </param>
        public SceneSourceLease (
            string scenePath,
            Scene scene,
            SceneTreeSourceStateKind sourceKind,
            bool closeAfterUse)
        {
            ScenePath = scenePath;
            Scene = scene;
            SourceKind = sourceKind;
            this.closeAfterUse = closeAfterUse;
        }

        /// <summary> Gets the logical scene asset path associated with the acquired source. </summary>
        public string ScenePath { get; }

        /// <summary> Gets the acquired scene source. </summary>
        public Scene Scene { get; }

        /// <summary> Gets the source kind that produced <see cref="Scene" />. </summary>
        public SceneTreeSourceStateKind SourceKind { get; }

        /// <summary> Creates the public source-state contract for this acquired scene source. </summary>
        /// <returns> The source-state contract. </returns>
        public SceneTreeSourceState CreateSourceState ()
        {
            return new SceneTreeSourceState(SourceKind, IsDirtySource(Scene, SourceKind));
        }

        /// <summary> Releases one transient preview scene when the acquisition policy requires cleanup. </summary>
        public void Dispose ()
        {
            if (!closeAfterUse)
            {
                return;
            }

            PersistedPreviewSceneLease.CloseIfNeeded(Scene);
        }

        private static bool IsDirtySource (
            Scene scene,
            SceneTreeSourceStateKind sourceKind)
        {
            return SceneTreeSourceStatePolicy.IsLiveSourceKind(sourceKind)
                && scene.isDirty;
        }
    }
}
