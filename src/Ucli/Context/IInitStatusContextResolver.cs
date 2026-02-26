namespace MackySoft.Ucli.Context;

/// <summary> Resolves foundation context shared by <c>init</c> and future <c>status</c> implementations. </summary>
internal interface IInitStatusContextResolver
{
    /// <summary> Resolves UnityProject and config values into an execution context. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the context-resolution result that contains either a fully resolved context or a structured error. </returns>
    ValueTask<InitStatusContextResolutionResult> Resolve (
        string? projectPath,
        CancellationToken cancellationToken = default);
}