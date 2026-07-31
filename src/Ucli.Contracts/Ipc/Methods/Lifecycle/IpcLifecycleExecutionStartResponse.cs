using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Returns the durable provider-observed binding of one Lifecycle Execution. </summary>
public sealed record IpcLifecycleExecutionStartResponse
{
    /// <summary> Initializes one response from the persisted start binding. </summary>
    [JsonConstructor]
    public IpcLifecycleExecutionStartResponse (LifecycleExecutionStartBinding start)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
    }

    /// <summary> Gets the binding persisted before any action side effect may begin. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionStartBinding Start { get; private init; }
}
