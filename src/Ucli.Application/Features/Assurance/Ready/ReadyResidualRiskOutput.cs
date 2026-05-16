namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one residual risk entry in a ready assurance payload. </summary>
internal sealed record ReadyResidualRiskOutput (
    string Code,
    bool Blocking,
    string? Message = null);
