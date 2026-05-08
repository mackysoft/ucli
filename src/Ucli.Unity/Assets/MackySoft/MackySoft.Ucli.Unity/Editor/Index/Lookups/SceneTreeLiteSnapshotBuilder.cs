using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Project;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds one deterministic scene-tree-lite snapshot from current scene state. </summary>
    internal sealed class SceneTreeLiteSnapshotBuilder : ISceneTreeLiteSnapshotBuilder
    {
        /// <inheritdoc />
        public ValueTask<IpcIndexSceneTreeLiteReadResponse> BuildAsync (
            string scenePath,
            bool loadedSceneOnly = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path must not be empty.", nameof(scenePath));
            }

            var normalizedScenePath = UnityAssetPathUtility.NormalizeAssetPath(scenePath);
            var policy = loadedSceneOnly
                ? SceneSourceResolver.Policy.LoadedOnly
                : SceneSourceResolver.Policy.LoadedOrPersistedPreview;
            if (!SceneSourceResolver.TryAcquire(
                    normalizedScenePath,
                    policy,
                    executionContext: null,
                    out var sceneLease,
                    out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(scenePath));
            }

            using (sceneLease)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var roots = SceneTreeNodeSnapshotBuilder.BuildRoots(sceneLease.Scene, depth: null, executionContext: null);
                return new ValueTask<IpcIndexSceneTreeLiteReadResponse>(new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    ScenePath: normalizedScenePath,
                    Roots: roots,
                    SourceState: sceneLease.CreateSourceState()));
            }
        }
    }
}
