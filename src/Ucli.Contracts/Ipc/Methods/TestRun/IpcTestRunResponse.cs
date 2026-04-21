namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>test.run</c> IPC response payload. </summary>
/// <param name="ExitCode"> The Unity test-run process-compatible exit code. </param>
public sealed record IpcTestRunResponse (
    int ExitCode);