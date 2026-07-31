using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed <c>compile</c> Lifecycle Execution request. </summary>
public sealed record IpcCompileRequest
{
    /// <summary> Initializes one request from the durable compile start binding. </summary>
    [JsonConstructor]
    public IpcCompileRequest (LifecycleExecutionStartBinding start)
    {
        Start = IpcLifecycleExecutionContractGuard.RequireStart(
            start,
            LifecycleExecutionKind.Compile,
            nameof(start));
    }

    /// <summary> Gets the durable facts fixed before the compile side effect. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionStartBinding Start { get; private init; }
}
