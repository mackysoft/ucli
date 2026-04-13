using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Phases;
using UnityEditor.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds one deterministic scene-tree-lite snapshot for a scene asset. </summary>
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
            if (!SceneOperationUtilities.TryEnsureSceneAssetExists(normalizedScenePath, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(scenePath));
            }

            var scene = EditorSceneManager.OpenScene(normalizedScenePath, OpenSceneMode.Single);
            cancellationToken.ThrowIfCancellationRequested();

            var roots = SceneTreeNodeSnapshotBuilder.BuildRoots(scene, depth: null, executionContext: null);
            return new ValueTask<IpcIndexSceneTreeLiteReadResponse>(new IpcIndexSceneTreeLiteReadResponse(
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                ScenePath: normalizedScenePath,
                Roots: roots));
        }
    }
}
