using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides scene-tree-lite source hashes from pre-resolved guarded scene and meta-file paths. </summary>
internal interface IReadIndexSceneSourceHashProvider
{
    /// <summary> Tries to compute one source hash from the current contents of both guarded source files. </summary>
    ValueTask<Sha256Digest?> TryComputeAsync (
        SceneTreeLiteSourcePaths sourcePaths,
        CancellationToken cancellationToken = default);
}
