using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one build assurance residual risk. </summary>
internal sealed record BuildResidualRiskOutput : IAssuranceVerdictResidualRisk
{
    public BuildResidualRiskOutput (
        UcliCode Code,
        UcliDiagnosticSeverity Severity,
        bool Blocking,
        string Message)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        if (!TextVocabulary.IsDefined(Severity))
        {
            throw new ArgumentOutOfRangeException(nameof(Severity), Severity, "Build residual-risk severity must be defined.");
        }

        this.Severity = Severity;
        this.Blocking = Blocking;
        this.Message = string.IsNullOrWhiteSpace(Message)
            ? throw new ArgumentException("Build residual-risk message must not be empty.", nameof(Message))
            : Message;
    }

    public UcliCode Code { get; }

    public UcliDiagnosticSeverity Severity { get; }

    public bool Blocking { get; }

    public string Message { get; }
}
