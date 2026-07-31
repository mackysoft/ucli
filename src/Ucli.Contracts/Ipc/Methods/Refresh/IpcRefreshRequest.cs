using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed <c>project.refresh</c> Lifecycle Execution request. </summary>
public sealed record IpcRefreshRequest
{
    /// <summary> Initializes one request from the durable refresh start binding. </summary>
    [JsonConstructor]
    public IpcRefreshRequest (LifecycleExecutionStartBinding start)
    {
        Start = IpcLifecycleExecutionContractGuard.RequireStart(
            start,
            LifecycleExecutionKind.Refresh,
            nameof(start));
    }

    /// <summary> Gets the durable facts fixed before the refresh side effect. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionStartBinding Start { get; private init; }
}
