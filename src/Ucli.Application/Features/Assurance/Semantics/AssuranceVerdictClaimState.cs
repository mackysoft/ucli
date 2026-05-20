namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Represents the claim fields used to calculate an assurance verdict. </summary>
internal readonly record struct AssuranceVerdictClaimState (
    string Status,
    string Coverage,
    bool Required,
    bool HasBlockingResidualRisk);
