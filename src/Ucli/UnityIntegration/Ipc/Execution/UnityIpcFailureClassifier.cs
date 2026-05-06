using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Converts IPC adapter failures into application-level Unity request failures. </summary>
internal static class UnityIpcFailureClassifier
{
    /// <summary> Creates a timeout failure. </summary>
    /// <param name="message"> The user-facing timeout message. </param>
    /// <returns> The classified timeout failure. </returns>
    public static UnityRequestFailure Timeout (string message)
    {
        return new UnityRequestFailure(
            ExecutionErrorCodes.IpcTimeout,
            message,
            ApplicationOutcome.ToolError);
    }

    /// <summary> Creates an internal failure. </summary>
    /// <param name="message"> The user-facing internal failure message. </param>
    /// <returns> The classified internal failure. </returns>
    public static UnityRequestFailure InternalError (string message)
    {
        return new UnityRequestFailure(
            IpcErrorCodes.InternalError,
            message,
            ApplicationOutcome.ToolError);
    }

    /// <summary> Converts a structured execution error into a Unity request failure. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The classified Unity request failure. </returns>
    public static UnityRequestFailure FromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return FromCodeAndMessage(
            ExecutionErrorCodeMapper.ToCode(error.Kind),
            error.Message);
    }

    /// <summary> Converts a mode-decision contract error into a Unity request failure. </summary>
    /// <param name="error"> The mode-decision contract error. </param>
    /// <returns> The classified Unity request failure. </returns>
    public static UnityRequestFailure FromModeDecisionContractError (UnityExecutionModeDecisionContractError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return FromCodeAndMessage(error.Code, error.Message);
    }

    /// <summary> Converts a daemon dispatch exception into a Unity request failure. </summary>
    /// <param name="exception"> The observed exception. </param>
    /// <param name="timeout"> The dispatch timeout. </param>
    /// <returns> The classified Unity request failure. </returns>
    public static UnityRequestFailure FromDaemonDispatchException (
        Exception exception,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (exception is TimeoutException)
        {
            return Timeout($"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
        }

        if (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return FromCodeAndMessage(
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                $"Unity daemon is not running. {exception.Message}");
        }

        return InternalError($"Failed to execute Unity daemon IPC request. {exception.Message}");
    }

    /// <summary> Converts a oneshot dispatch exception into a Unity request failure. </summary>
    /// <param name="exception"> The observed exception. </param>
    /// <param name="timeout"> The dispatch timeout. </param>
    /// <returns> The classified Unity request failure. </returns>
    public static UnityRequestFailure FromOneshotDispatchException (
        Exception exception,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (exception is TimeoutException)
        {
            return OneshotTimeout(timeout);
        }

        return InternalError($"Failed to execute Unity oneshot IPC request. {exception.Message}");
    }

    /// <summary> Creates a daemon-not-running failure from one probing exception. </summary>
    /// <param name="exception"> The probing exception. </param>
    /// <returns> The classified daemon-not-running failure. </returns>
    public static UnityRequestFailure DaemonNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FromCodeAndMessage(
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
            $"Unity daemon is not running. {exception.Message}");
    }

    /// <summary> Creates a oneshot timeout failure. </summary>
    /// <param name="timeout"> The timeout budget. </param>
    /// <returns> The classified oneshot timeout failure. </returns>
    public static UnityRequestFailure OneshotTimeout (TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        return Timeout($"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
    }

    /// <summary> Creates a failure from one error code and message. </summary>
    /// <param name="code"> The machine-readable failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The classified Unity request failure. </returns>
    public static UnityRequestFailure FromCodeAndMessage (
        string code,
        string message)
    {
        return new UnityRequestFailure(
            code,
            message,
            ResolveOutcome(code));
    }

    private static ApplicationOutcome ResolveOutcome (string code)
    {
        return string.Equals(code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
            ? ApplicationOutcome.InvalidArgument
            : ApplicationOutcome.ToolError;
    }
}
