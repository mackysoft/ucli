using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Represents the result of resolving daemon command execution context values. </summary>
/// <param name="Context"> The resolved execution context on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonCommandExecutionContextResolutionResult (
    DaemonCommandExecutionContext? Context,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether execution-context resolution succeeded. </summary>
    public bool IsSuccess => Context is not null && Error is null;

    /// <summary> Creates a successful daemon-command execution-context resolution result. </summary>
    /// <param name="context"> The resolved daemon-command execution context. </param>
    /// <returns> The successful daemon-command execution-context resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public static DaemonCommandExecutionContextResolutionResult Success (DaemonCommandExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new DaemonCommandExecutionContextResolutionResult(context, null);
    }

    /// <summary> Creates a failed daemon-command execution-context resolution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed daemon-command execution-context resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonCommandExecutionContextResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonCommandExecutionContextResolutionResult(null, error);
    }
}