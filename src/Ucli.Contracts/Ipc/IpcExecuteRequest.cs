using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC request payload. </summary>
/// <param name="Command"> The command identifier to execute on Unity side. </param>
/// <param name="Arguments"> The command argument payload. </param>
public sealed record IpcExecuteRequest (
    string Command,
    JsonElement Arguments);
