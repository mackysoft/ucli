namespace MackySoft.Ucli.Execution;

/// <summary> Represents one process execution result. </summary>
/// <param name="Status"> The process status. </param>
/// <param name="ExitCode"> The process exit code when available. </param>
/// <param name="ErrorMessage"> The process error message when available. </param>
internal sealed record ProcessRunResult (
    ProcessRunStatus Status,
    int? ExitCode,
    string? ErrorMessage)
{
    /// <summary> Creates one start-failed process result. </summary>
    /// <param name="errorMessage"> The failure message. </param>
    /// <returns> The start-failed result. </returns>
    public static ProcessRunResult StartFailed (string errorMessage)
    {
        return new ProcessRunResult(ProcessRunStatus.StartFailed, null, errorMessage);
    }

    /// <summary> Creates one timeout process result. </summary>
    /// <param name="errorMessage"> The timeout message. </param>
    /// <returns> The timeout result. </returns>
    public static ProcessRunResult TimedOut (string errorMessage)
    {
        return new ProcessRunResult(ProcessRunStatus.TimedOut, null, errorMessage);
    }

    /// <summary> Creates one canceled process result. </summary>
    /// <param name="errorMessage"> The canceled message. </param>
    /// <returns> The canceled result. </returns>
    public static ProcessRunResult Canceled (string errorMessage)
    {
        return new ProcessRunResult(ProcessRunStatus.Canceled, null, errorMessage);
    }

    /// <summary> Creates one exited process result. </summary>
    /// <param name="exitCode"> The process exit code. </param>
    /// <param name="errorMessage"> The optional error message. </param>
    /// <returns> The exited result. </returns>
    public static ProcessRunResult Exited (int exitCode, string? errorMessage = null)
    {
        return new ProcessRunResult(ProcessRunStatus.Exited, exitCode, errorMessage);
    }
}