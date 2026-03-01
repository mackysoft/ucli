using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC request payload. </summary>
/// <param name="Command"> The command identifier to execute on Unity side. </param>
/// <param name="Arguments"> The command argument payload. </param>
public sealed record IpcExecuteRequest (
    string Command,
    JsonElement Arguments)
{
    /// <summary> Gets the optional plan token used for call-time drift validation. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanToken { get; init; }
}