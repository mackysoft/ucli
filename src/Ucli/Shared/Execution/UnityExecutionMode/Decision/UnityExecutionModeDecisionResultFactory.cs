using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Creates mode decision results from parsed mode input and daemon state. </summary>
internal static class UnityExecutionModeDecisionResultFactory
{
    internal const string InvalidModeMessage = "Mode must be auto, daemon, or oneshot.";

    private const string DaemonNotRunningMessage = "Daemon is not running for mode=daemon.";

    private const string DaemonRunningOneshotForbiddenMessage = "Daemon is running for mode=oneshot.";

    /// <summary> Creates an invalid-argument result for unsupported mode input. </summary>
    /// <returns> The mode decision result that contains an invalid-argument error. </returns>
    public static UnityExecutionModeDecisionResult InvalidMode ()
    {
        return UnityExecutionModeDecisionResult.Failure(CreateInvalidModeError());
    }

    /// <summary> Creates an invalid-argument error for unsupported mode input. </summary>
    /// <returns> The invalid-mode error. </returns>
    public static ExecutionError CreateInvalidModeError ()
    {
        return ExecutionError.InvalidArgument(InvalidModeMessage);
    }

    /// <summary> Creates an infrastructure-failure result from probe errors. </summary>
    /// <param name="error"> The infrastructure error from probing daemon reachability. </param>
    /// <returns> The mode decision result that contains the specified infrastructure error. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityExecutionModeDecisionResult ProbeFailure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return UnityExecutionModeDecisionResult.Failure(error);
    }

    /// <summary> Creates a decision result from requested mode and daemon running state. </summary>
    /// <param name="requestedMode"> The parsed requested mode. </param>
    /// <param name="daemonRunning"> Whether daemon is reachable. </param>
    /// <param name="timeout"> The resolved timeout applied to probing and request execution. </param>
    /// <returns> The resolved mode decision result. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="requestedMode" /> is outside supported values. </exception>
    public static UnityExecutionModeDecisionResult FromRequestedMode (
        UnityExecutionMode requestedMode,
        bool daemonRunning,
        TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var contractError = ResolveContractError(requestedMode, daemonRunning);
        if (contractError is not null)
        {
            return UnityExecutionModeDecisionResult.ContractFailure(contractError);
        }

        var target = ResolveTarget(requestedMode, daemonRunning);
        var decision = new UnityExecutionModeDecision(requestedMode, daemonRunning, target, timeout);
        return UnityExecutionModeDecisionResult.Success(decision);
    }

    /// <summary> Resolves contract error for one requested mode and daemon running state. </summary>
    /// <param name="requestedMode"> The parsed requested mode. </param>
    /// <param name="daemonRunning"> Whether daemon is reachable. </param>
    /// <returns> The contract error when the requested mode is forbidden; otherwise <see langword="null" />. </returns>
    private static UnityExecutionModeDecisionContractError? ResolveContractError (
        UnityExecutionMode requestedMode,
        bool daemonRunning)
    {
        if (requestedMode == UnityExecutionMode.Daemon && !daemonRunning)
        {
            return new UnityExecutionModeDecisionContractError(
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                DaemonNotRunningMessage);
        }

        if (requestedMode == UnityExecutionMode.Oneshot && daemonRunning)
        {
            return new UnityExecutionModeDecisionContractError(
                UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden,
                DaemonRunningOneshotForbiddenMessage);
        }

        return null;
    }

    /// <summary> Resolves execution target from requested mode and daemon running state. </summary>
    /// <param name="requestedMode"> The parsed requested mode. </param>
    /// <param name="daemonRunning"> Whether daemon is reachable. </param>
    /// <returns> The resolved execution target. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="requestedMode" /> is outside supported values. </exception>
    private static UnityExecutionTarget ResolveTarget (
        UnityExecutionMode requestedMode,
        bool daemonRunning)
    {
        return requestedMode switch
        {
            UnityExecutionMode.Auto when daemonRunning => UnityExecutionTarget.Daemon,
            UnityExecutionMode.Auto => UnityExecutionTarget.Oneshot,
            UnityExecutionMode.Daemon => UnityExecutionTarget.Daemon,
            UnityExecutionMode.Oneshot => UnityExecutionTarget.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedMode), requestedMode, "Unsupported execution mode."),
        };
    }
}
