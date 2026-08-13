using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> The closed disposition of a Program request start or attach operation. </summary>
public enum IpcProgramRequestExecutionStatus { Running = 1, Terminal, NotStarted, Conflict, GenerationMismatch, Unavailable }

/// <summary> Returns a retained Program Request terminal response without replaying its side effect. </summary>
public sealed record IpcProgramRequestExecutionResponse
{
    [JsonConstructor]
    public IpcProgramRequestExecutionResponse (
        IpcProgramRequestExecutionStatus status,
        Guid executionId,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot generation,
        byte[]? responseBytes = null)
    {
        if (!Enum.IsDefined(typeof(IpcProgramRequestExecutionStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (executionId == Guid.Empty) throw new ArgumentException("Execution id must not be empty.", nameof(executionId));
        Status = status;
        ExecutionId = executionId;
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
        ResponseBytes = responseBytes is null ? null : (byte[])responseBytes.Clone();
        if (status == IpcProgramRequestExecutionStatus.Terminal && ResponseBytes is null)
        {
            throw new ArgumentException("A terminal Program Request response requires exact response bytes.", nameof(responseBytes));
        }
        if (status != IpcProgramRequestExecutionStatus.Terminal && ResponseBytes is not null)
        {
            throw new ArgumentException("Only a terminal Program Request response may carry response bytes.", nameof(responseBytes));
        }
    }

    [JsonInclude, JsonRequired] public IpcProgramRequestExecutionStatus Status { get; private init; }
    [JsonInclude, JsonRequired] public Guid ExecutionId { get; private init; }
    [JsonInclude, JsonRequired] public LifecycleExecutionHostRegistration Host { get; private init; }
    [JsonInclude, JsonRequired] public UnityEditorGenerationSnapshot Generation { get; private init; }
    [JsonInclude] public byte[]? ResponseBytes { get; private init; }
}
