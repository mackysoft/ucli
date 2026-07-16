namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Represents one structured contract error emitted from mode decision. </summary>
/// <param name="Code"> The machine-readable error code. </param>
/// <param name="Message"> The human-readable error message. </param>
internal sealed record UnityExecutionModeDecisionContractError
{
    public UnityExecutionModeDecisionContractError (
        UcliCode Code,
        string Message)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Message = Message;
    }

    public UcliCode Code { get; }

    public string Message { get; }
}
