namespace MackySoft.Ucli.Execution.OperationExecute;

/// <summary> Executes one fixed operation through the shared CLI request pipeline. </summary>
internal interface IOperationExecuteService
{
    /// <summary> Executes the specified fixed operation and returns the normalized execution result. </summary>
    /// <param name="definition"> The fixed operation definition to execute. </param>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="mode"> The optional <c>--mode</c> value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
    /// <param name="waitUntilReady"> Whether Unity-side execution may wait for lifecycle readiness before failing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized operation execution result. </returns>
    ValueTask<OperationExecuteResult> Execute (
        OperationExecuteDefinition definition,
        string? projectPath,
        string? mode,
        string? timeout,
        bool waitUntilReady,
        CancellationToken cancellationToken = default);
}