using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one application-level operation execution error. </summary>
/// <param name="Code"> The machine-readable error code. </param>
/// <param name="Message"> The human-readable error message. </param>
/// <param name="InstancePath"> The RFC 6901 path of the related value, or <see langword="null" /> when not applicable. </param>
internal sealed record OperationExecutionError
{
    public OperationExecutionError (
        UcliCode Code,
        string Message,
        string? InstancePath)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        ArgumentException.ThrowIfNullOrWhiteSpace(Message);
        this.Message = Message;
        this.InstancePath = InstancePath;
    }

    public UcliCode Code { get; }

    public string Message { get; }

    public string? InstancePath { get; }
}
