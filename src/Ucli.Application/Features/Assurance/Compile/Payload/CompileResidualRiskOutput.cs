using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents one residual risk entry in a compile assurance payload. </summary>
internal sealed record CompileResidualRiskOutput : IAssuranceVerdictResidualRisk
{
    public CompileResidualRiskOutput (
        UcliCode Code,
        bool Blocking,
        string Message)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Blocking = Blocking;
        this.Message = string.IsNullOrWhiteSpace(Message)
            ? throw new ArgumentException("Compile residual-risk message must not be empty.", nameof(Message))
            : Message;
    }

    public UcliCode Code { get; }

    public bool Blocking { get; }

    public string Message { get; }
}
