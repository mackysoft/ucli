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
    /// <summary> Gets a value indicating whether Play Mode mutation is explicitly allowed for this execute request. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowPlayMode { get; init; }

    /// <summary> Gets a value indicating whether dangerous operations are explicitly allowed for this execute request. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowDangerous { get; init; }

    /// <summary> Gets a value indicating whether execution should fail immediately instead of waiting for lifecycle readiness. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FailFast { get; init; }

    /// <summary> Gets the optional plan token used for call-time drift validation. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanToken { get; init; }

    /// <summary> Gets the optional request execution timeout in milliseconds. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
