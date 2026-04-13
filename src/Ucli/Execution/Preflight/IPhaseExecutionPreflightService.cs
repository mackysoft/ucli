namespace MackySoft.Ucli.Execution;

/// <summary> Executes static preflight for one already prepared phase-execution request. </summary>
internal interface IPhaseExecutionPreflightService
{
    /// <summary> Executes static preflight and returns a prepared request or structured errors. </summary>
    /// <param name="preparedRequest"> The request that has already been read, parsed, and bound to project context. </param>
    /// <param name="mode"> The normalized requested Unity execution mode. </param>
    /// <param name="deadline"> The shared timeout budget for the surrounding command execution. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    ValueTask<PhaseExecutionPreflightResult> Prepare (
        PreparedRequestContext preparedRequest,
        UnityExecutionMode mode,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}