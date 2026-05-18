namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC response payload. </summary>
/// <param name="RunId"> The compile run identifier. </param>
/// <param name="Summary"> The completed compile summary. </param>
public sealed record IpcCompileResponse (
    string RunId,
    IpcCompileSummary Summary);
