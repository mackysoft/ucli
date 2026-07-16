using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one validated machine-readable IPC error entry. </summary>
public sealed record IpcError
{
    /// <summary> Initializes one IPC error entry. </summary>
    /// <param name="Code"> The error code that identifies the failure type. </param>
    /// <param name="Message"> The non-empty human-readable error message. </param>
    /// <param name="OpId"> The non-empty related operation identifier, or <see langword="null" /> when not applicable. </param>
    /// <exception cref="ArgumentException"> Thrown when an argument does not satisfy its value contract. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Code" /> or <paramref name="Message" /> is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcError (
        UcliCode Code,
        string Message,
        IpcExecuteStepId? OpId)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Message = ContractArgumentGuard.RequireValue(Message, nameof(Message));
        this.OpId = OpId;
    }

    /// <summary> Gets the error code that identifies the failure type. </summary>
    public UcliCode Code { get; }

    /// <summary> Gets the human-readable error message. </summary>
    public string Message { get; }

    /// <summary> Gets the related operation identifier, or <see langword="null" /> when not applicable. </summary>
    public IpcExecuteStepId? OpId { get; }
}
