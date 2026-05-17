namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents one residual risk entry in a compile assurance payload. </summary>
internal sealed record CompileResidualRiskOutput (
    string Code,
    bool Blocking);
