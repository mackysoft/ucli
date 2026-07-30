namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Exposes the typed residual-risk fact that determines an assurance verdict. </summary>
internal interface IAssuranceVerdictResidualRisk
{
    bool Blocking { get; }
}
