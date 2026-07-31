using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed <c>play.exit</c> Lifecycle Execution request. </summary>
public sealed record IpcPlayExitRequest
{
    /// <summary> Initializes one request from the durable Play Mode exit start binding. </summary>
    [JsonConstructor]
    public IpcPlayExitRequest (LifecycleExecutionStartBinding start)
    {
        Start = IpcLifecycleExecutionContractGuard.RequireStart(
            start,
            LifecycleExecutionKind.PlayExit,
            nameof(start));
    }

    /// <summary> Gets the durable facts fixed before the Play Mode exit side effect. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionStartBinding Start { get; private init; }
}
