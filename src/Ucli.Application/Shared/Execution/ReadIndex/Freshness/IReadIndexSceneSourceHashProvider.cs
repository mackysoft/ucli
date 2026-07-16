using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides scene-tree-lite source hashes without exposing filesystem traversal details to application policy. </summary>
internal interface IReadIndexSceneSourceHashProvider
{
    /// <summary> Tries to compute one source hash for a scene-tree-lite lookup source. </summary>
    ValueTask<Sha256Digest?> TryComputeAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default);
}
