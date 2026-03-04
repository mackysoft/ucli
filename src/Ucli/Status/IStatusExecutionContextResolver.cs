namespace MackySoft.Ucli.Status;

/// <summary> Resolves preflight execution context values required by the status workflow. </summary>
internal interface IStatusExecutionContextResolver
{
    /// <summary> Resolves context, timeout, and Unity version values for one status execution. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the execution-context resolution result. </returns>
    ValueTask<StatusExecutionContextResolutionResult> Resolve (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}