namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides scene-tree-lite source hashes without exposing filesystem traversal details to application policy. </summary>
internal interface IReadIndexSceneSourceHashProvider
{
    /// <summary> Tries to compute one source hash for a scene-tree-lite lookup source. </summary>
    ValueTask<string?> TryCompute (
        string projectRootPath,
        string scenePath,
        CancellationToken cancellationToken = default);
}
