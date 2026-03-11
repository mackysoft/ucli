namespace MackySoft.Ucli.Supervisor;

/// <summary> Represents one completed external process invocation used by supervisor bootstrap. </summary>
internal sealed record SupervisorExternalProcessExecutionResult (
    int ExitCode,
    string StandardOutput,
    string StandardError);