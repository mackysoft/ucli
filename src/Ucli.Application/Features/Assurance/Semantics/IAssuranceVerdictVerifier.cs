namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Exposes the typed verifier facts that establish an assurance aggregate. </summary>
internal interface IAssuranceVerdictVerifier
{
    AssuranceVerifierId Id { get; }

    bool Required { get; }

    IReadOnlyList<UcliCode> PrimaryClaims { get; }
}
