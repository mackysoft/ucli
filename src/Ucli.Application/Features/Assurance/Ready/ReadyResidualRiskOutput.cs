using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one residual risk entry in a ready assurance payload. </summary>
internal sealed record ReadyResidualRiskOutput : IAssuranceVerdictResidualRisk
{
    public ReadyResidualRiskOutput (
        UcliCode Code,
        bool Blocking,
        string Message)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Blocking = Blocking;
        this.Message = string.IsNullOrWhiteSpace(Message)
            ? throw new ArgumentException("Ready residual-risk message must not be empty.", nameof(Message))
            : Message;
    }

    public UcliCode Code { get; }

    public bool Blocking { get; }

    public string Message { get; }
}
