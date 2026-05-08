using System.Text.Json;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents a host-executed Unity request without owning the IPC wire envelope. </summary>
internal abstract record UnityRequestPayload
{
    /// <summary> Represents a request whose method and payload are already owned by a host adapter. </summary>
    internal sealed record Raw (
        string Method,
        JsonElement Payload) : UnityRequestPayload;

    /// <summary> Represents an execute request whose execute-arguments JSON was already prepared. </summary>
    internal sealed record ExecuteJson (
        UcliCommand Command,
        JsonElement ExecuteArguments,
        bool FailFast,
        bool AllowDangerous = false,
        string? PlanToken = null) : UnityRequestPayload;

    /// <summary> Represents a single-operation execute request prepared by application orchestration. </summary>
    internal sealed record ExecuteOperation (
        UcliCommand Command,
        string RequestId,
        string OperationId,
        string OperationName,
        JsonElement Args,
        bool FailFast,
        bool AllowDangerous = false,
        string? PlanToken = null) : UnityRequestPayload;
}
