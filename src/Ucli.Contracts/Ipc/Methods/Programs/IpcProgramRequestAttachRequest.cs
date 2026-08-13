using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies one existing Program request execution for attach-only recovery. </summary>
public sealed record IpcProgramRequestAttachRequest
{
    [JsonConstructor]
    public IpcProgramRequestAttachRequest (Guid executionId, IpcProgramRequestExecutionBinding binding)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Execution id must not be empty.", nameof(executionId));
        ExecutionId = executionId;
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
    }

    [JsonInclude]
    [JsonRequired]
    public Guid ExecutionId { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramRequestExecutionBinding Binding { get; private init; }
}
