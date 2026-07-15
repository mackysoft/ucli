namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one machine-readable index service error. </summary>
/// <param name="Code"> The error code. </param>
/// <param name="Message"> The user-facing error message. </param>
internal sealed record IndexServiceError
{
    public IndexServiceError (
        UcliCode Code,
        string Message)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Message = Message;
    }

    public UcliCode Code { get; }

    public string Message { get; }
}
