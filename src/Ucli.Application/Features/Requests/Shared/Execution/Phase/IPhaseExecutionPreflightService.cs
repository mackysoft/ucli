using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;

/// <summary> Executes static preflight for one already prepared phase-execution request. </summary>
internal interface IPhaseExecutionPreflightService
{
    /// <summary> Executes static preflight and returns a prepared request or structured errors. </summary>
    /// <param name="preparedRequest"> The request that has already been read, parsed, and bound to project context. </param>
    /// <param name="mode"> The normalized requested Unity execution mode. </param>
    /// <param name="deadline"> The shared timeout budget for the surrounding command execution. </param>
    /// <param name="failFast"> Whether operation metadata discovery should fail immediately instead of waiting for Unity readiness. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    ValueTask<PhaseExecutionPreflightResult> PrepareAsync (
        PreparedRequestContext preparedRequest,
        UnityExecutionMode mode,
        ExecutionDeadline deadline,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
