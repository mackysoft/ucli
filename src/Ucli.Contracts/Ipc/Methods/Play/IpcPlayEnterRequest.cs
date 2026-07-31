using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed <c>play.enter</c> Lifecycle Execution request. </summary>
public sealed record IpcPlayEnterRequest
{
    /// <summary> Initializes one request from the durable Play Mode entry start binding. </summary>
    [JsonConstructor]
    public IpcPlayEnterRequest (LifecycleExecutionStartBinding start)
    {
        Start = IpcLifecycleExecutionContractGuard.RequireStart(
            start,
            LifecycleExecutionKind.PlayEnter,
            nameof(start));
    }

    /// <summary> Gets the durable facts fixed before the Play Mode entry side effect. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionStartBinding Start { get; private init; }
}
