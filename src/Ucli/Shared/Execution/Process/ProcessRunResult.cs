namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Represents one process execution result. </summary>
/// <param name="Status"> The process status. </param>
/// <param name="ExitCode"> The process exit code when available. </param>
/// <param name="ErrorMessage"> The process error message when available. </param>
/// <param name="StandardOutput"> The full captured standard-output text when requested by the caller. </param>
internal sealed record ProcessRunResult (
    ProcessRunStatus Status,
    int? ExitCode,
    string? ErrorMessage,
    string? StandardOutput)
{
    /// <summary> Creates one start-failed process result. </summary>
    /// <param name="errorMessage"> The failure message. </param>
    /// <returns> The start-failed result. </returns>
    public static ProcessRunResult StartFailed (
        string errorMessage,
        string? standardOutput = null)
    {
        return new ProcessRunResult(ProcessRunStatus.StartFailed, null, errorMessage, standardOutput);
    }

    /// <summary> Creates one timeout process result. </summary>
    /// <param name="errorMessage"> The timeout message. </param>
    /// <returns> The timeout result. </returns>
    public static ProcessRunResult TimedOut (
        string errorMessage,
        string? standardOutput = null)
    {
        return new ProcessRunResult(ProcessRunStatus.TimedOut, null, errorMessage, standardOutput);
    }

    /// <summary> Creates one canceled process result. </summary>
    /// <param name="errorMessage"> The canceled message. </param>
    /// <returns> The canceled result. </returns>
    public static ProcessRunResult Canceled (
        string errorMessage,
        string? standardOutput = null)
    {
        return new ProcessRunResult(ProcessRunStatus.Canceled, null, errorMessage, standardOutput);
    }

    /// <summary> Creates one exited process result. </summary>
    /// <param name="exitCode"> The process exit code. </param>
    /// <param name="errorMessage"> The optional error message. </param>
    /// <returns> The exited result. </returns>
    public static ProcessRunResult Exited (
        int exitCode,
        string? errorMessage = null,
        string? standardOutput = null)
    {
        return new ProcessRunResult(ProcessRunStatus.Exited, exitCode, errorMessage, standardOutput);
    }
}
