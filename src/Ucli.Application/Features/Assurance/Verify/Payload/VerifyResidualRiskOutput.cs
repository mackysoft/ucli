using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one residual risk in a verify assurance payload. </summary>
internal sealed record VerifyResidualRiskOutput : IAssuranceVerdictResidualRisk
{
    public VerifyResidualRiskOutput (
        UcliCode Code,
        bool Blocking,
        string Message)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Blocking = Blocking;
        this.Message = string.IsNullOrWhiteSpace(Message)
            ? throw new ArgumentException("Verify residual-risk message must not be empty.", nameof(Message))
            : Message;
    }

    public UcliCode Code { get; }

    public bool Blocking { get; }

    public string Message { get; }
}
