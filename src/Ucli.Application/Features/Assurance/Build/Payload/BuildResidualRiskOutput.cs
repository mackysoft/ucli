namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one build assurance residual risk. </summary>
internal sealed record BuildResidualRiskOutput (
    string Code,
    UcliDiagnosticSeverity Severity,
    bool Blocking,
    string Statement);
