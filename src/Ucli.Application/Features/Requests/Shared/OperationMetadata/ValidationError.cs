using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one validation error entry for static request validation. </summary>
/// <param name="Code"> The machine-readable validation code. </param>
/// <param name="Message"> The validation detail message. </param>
/// <param name="OpId"> The operation identifier related to this error, or <see langword="null" /> when not associated. </param>
internal sealed record ValidationError
{
    public ValidationError (
        UcliCode Code,
        string Message,
        IpcExecuteStepId? OpId)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        ArgumentException.ThrowIfNullOrWhiteSpace(Message);
        this.Message = Message;
        this.OpId = OpId;
    }

    public UcliCode Code { get; }

    public string Message { get; }

    public IpcExecuteStepId? OpId { get; }
}
