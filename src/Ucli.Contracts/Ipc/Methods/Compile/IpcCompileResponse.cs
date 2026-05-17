namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC response payload. </summary>
/// <param name="RunId"> The compile run identifier. </param>
/// <param name="SummaryJsonPath"> The absolute path to the persisted compile summary artifact. </param>
/// <param name="DiagnosticsJsonPath"> The absolute path to the persisted diagnostics artifact when it exists. </param>
/// <param name="Summary"> The completed compile summary. </param>
public sealed record IpcCompileResponse (
    string RunId,
    string SummaryJsonPath,
    string? DiagnosticsJsonPath,
    IpcCompileSummary Summary);
