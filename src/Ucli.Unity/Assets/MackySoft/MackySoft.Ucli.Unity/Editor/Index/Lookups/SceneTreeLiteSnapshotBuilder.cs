using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Project;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds one deterministic scene-tree-lite snapshot from persisted preview scene contents. </summary>
    internal sealed class SceneTreeLiteSnapshotBuilder : ISceneTreeLiteSnapshotBuilder
    {
        /// <inheritdoc />
        public ValueTask<IpcIndexSceneTreeLiteReadResponse> Build (
            string scenePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("Scene path must not be empty.", nameof(scenePath));
            }

            var normalizedScenePath = UnityAssetPathUtility.NormalizeAssetPath(scenePath);
            if (!PersistedPreviewSceneLease.TryOpen(
                    normalizedScenePath,
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
                    Roots: roots));
            }
        }
    }
}
