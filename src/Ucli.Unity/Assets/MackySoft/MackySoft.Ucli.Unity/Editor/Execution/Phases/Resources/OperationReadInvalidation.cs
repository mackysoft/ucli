namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one read-surface invalidation emitted by a mutation operation. </summary>
    /// <param name="Surface"> The invalidated read surface. </param>
    /// <param name="ScenePath"> The normalized scene path when <see cref="Surface" /> is scene-scoped; otherwise <see langword="null" />. </param>
    internal sealed record OperationReadInvalidation (
        OperationReadInvalidationSurface Surface,
        string? ScenePath);
}