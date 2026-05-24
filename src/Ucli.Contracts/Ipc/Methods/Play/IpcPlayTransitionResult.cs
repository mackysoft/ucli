using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the result of one Play Mode lifecycle transition request. </summary>
/// <param name="Transition"> The requested transition command literal. </param>
/// <param name="Result"> The transition result literal. </param>
/// <param name="Before"> The lifecycle snapshot observed before issuing the transition request. </param>
public sealed record IpcPlayTransitionResult (
    string Transition,
    string Result,
    IpcPlayLifecycleSnapshot Before)
{
    /// <summary> Gets the lifecycle snapshot observed after a successful transition. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcPlayLifecycleSnapshot? After { get; init; }

    /// <summary> Gets the latest lifecycle snapshot observed for transition errors or timeouts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcPlayLifecycleSnapshot? Observed { get; init; }

    /// <summary> Gets the application-state literal for transition errors or timeouts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApplicationState { get; init; }
}
