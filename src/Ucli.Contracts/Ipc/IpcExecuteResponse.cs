using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC response payload. </summary>
/// <param name="Result"> The command execution result payload. </param>
public sealed record IpcExecuteResponse (
    JsonElement Result);
