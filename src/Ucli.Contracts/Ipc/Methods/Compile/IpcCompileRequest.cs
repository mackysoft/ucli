namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC request payload. </summary>
/// <param name="RunId"> The CLI-generated compile run identifier. </param>
public sealed record IpcCompileRequest (string RunId);
