using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines errors emitted by the Program-owned Request execution boundary. </summary>
public static class ProgramRequestExecutionErrorCodes
{
    /// <summary> Gets the error returned by a cancelled Program Request after its terminal response is retained. </summary>
    public static readonly UcliCode Cancelled = new("PROGRAM_REQUEST_CANCELLED");

    /// <summary> Gets the error returned when the Program Request deadline has cancelled its execution. </summary>
    public static readonly UcliCode DeadlineExceeded = new("PROGRAM_REQUEST_DEADLINE_EXCEEDED");
}

/// <summary> Identifies the immutable reason for requesting cancellation of one Program-owned Request execution. </summary>
public enum IpcProgramRequestCancellationReason { UserCancelled = 1, DeadlineExceeded }

/// <summary> Requests cancellation of one previously admitted Program Request without starting another execution. </summary>
public sealed record IpcProgramRequestCancelRequest
{
    [JsonConstructor]
    public IpcProgramRequestCancelRequest (
        Guid executionId,
        IpcProgramRequestExecutionBinding binding,
        IpcProgramRequestCancellationReason reason)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Execution id must not be empty.", nameof(executionId));
        if (!Enum.IsDefined(typeof(IpcProgramRequestCancellationReason), reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        ExecutionId = executionId;
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Reason = reason;
    }

    [JsonInclude]
    [JsonRequired]
    public Guid ExecutionId { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramRequestExecutionBinding Binding { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramRequestCancellationReason Reason { get; private init; }
}

/// <summary> Reports whether cancellation was delivered to the same Program Request execution. </summary>
public enum IpcProgramRequestCancellationStatus { Requested = 1, Terminal, NotStarted, Conflict, Unsupported, GenerationMismatch }

/// <summary> Returns a closed Program Request cancellation disposition without fabricating a terminal result. </summary>
public sealed record IpcProgramRequestCancelResponse
{
    [JsonConstructor]
    public IpcProgramRequestCancelResponse (
        IpcProgramRequestCancellationStatus status,
        Guid executionId,
        IpcProgramRequestCancellationReason reason)
    {
        if (!Enum.IsDefined(typeof(IpcProgramRequestCancellationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (executionId == Guid.Empty) throw new ArgumentException("Execution id must not be empty.", nameof(executionId));
        if (!Enum.IsDefined(typeof(IpcProgramRequestCancellationReason), reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        Status = status;
        ExecutionId = executionId;
        Reason = reason;
    }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramRequestCancellationStatus Status { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public Guid ExecutionId { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramRequestCancellationReason Reason { get; private init; }
}
