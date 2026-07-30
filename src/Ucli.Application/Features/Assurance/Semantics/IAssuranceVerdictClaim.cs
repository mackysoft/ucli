namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Exposes the typed claim facts that determine an assurance verdict. </summary>
internal interface IAssuranceVerdictClaim
{
    UcliCode Id { get; }

    AssuranceClaimStatus Status { get; }

    AssuranceCoverage Coverage { get; }

    bool Required { get; }

    AssuranceVerifierId VerifierRef { get; }

    bool HasBlockingResidualRisk { get; }
}
