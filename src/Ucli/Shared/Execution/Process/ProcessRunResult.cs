namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Represents one process execution result. </summary>
/// <param name="Status"> The process status. </param>
/// <param name="ExitCode"> The process exit code when available. </param>
/// <param name="ErrorMessage"> The process error message when available. </param>
/// <param name="StandardOutput"> The full captured standard-output text when requested by the caller. </param>
/// <param name="TerminationResult"> How process termination proceeded after timeout or cancellation. </param>
internal sealed record ProcessRunResult (
    ProcessRunStatus Status,
    int? ExitCode,
    string? ErrorMessage,
    string? StandardOutput,
    ProcessTerminationResult TerminationResult)
{
    /// <summary> Creates one start-failed process result. </summary>
    /// <param name="errorMessage"> The failure message. </param>
    /// <param name="standardOutput"> The full captured standard-output text when requested by the caller. </param>
    /// <returns> The start-failed result. </returns>
    public static ProcessRunResult StartFailed (
        string errorMessage,
        string? standardOutput = null)
    {
        return new ProcessRunResult(ProcessRunStatus.StartFailed, null, errorMessage, standardOutput, ProcessTerminationResult.None);
    }

    /// <summary> Creates one timeout process result. </summary>
    /// <param name="errorMessage"> The timeout message. </param>
    /// <param name="standardOutput"> The full captured standard-output text when requested by the caller. </param>
    /// <param name="terminationResult"> The termination result observed after timeout cleanup. </param>
    /// <returns> The timeout result. </returns>
    public static ProcessRunResult TimedOut (
        string errorMessage,
        string? standardOutput = null,
        ProcessTerminationResult terminationResult = ProcessTerminationResult.None)
    {
        return new ProcessRunResult(ProcessRunStatus.TimedOut, null, errorMessage, standardOutput, terminationResult);
    }

    /// <summary> Creates one canceled process result. </summary>
    /// <param name="errorMessage"> The canceled message. </param>
    /// <param name="standardOutput"> The full captured standard-output text when requested by the caller. </param>
    /// <param name="terminationResult"> The termination result observed after cancellation cleanup. </param>
    /// <returns> The canceled result. </returns>
    public static ProcessRunResult Canceled (
        string errorMessage,
        string? standardOutput = null,
        ProcessTerminationResult terminationResult = ProcessTerminationResult.None)
    {
        return new ProcessRunResult(ProcessRunStatus.Canceled, null, errorMessage, standardOutput, terminationResult);
    }

    /// <summary> Creates one exited process result. </summary>
    /// <param name="exitCode"> The process exit code. </param>
    /// <param name="errorMessage"> The optional error message. </param>
    /// <param name="standardOutput"> The full captured standard-output text when requested by the caller. </param>
    /// <returns> The exited result. </returns>
    public static ProcessRunResult Exited (
        int exitCode,
        string? errorMessage = null,
        string? standardOutput = null)
    {
        return new ProcessRunResult(ProcessRunStatus.Exited, exitCode, errorMessage, standardOutput, ProcessTerminationResult.None);
    }
}
