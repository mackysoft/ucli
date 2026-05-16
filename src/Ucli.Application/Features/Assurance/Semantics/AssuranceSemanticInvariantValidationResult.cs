namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Represents semantic invariant validation output for one assurance payload. </summary>
/// <param name="Violations"> The detected semantic invariant violations. </param>
internal sealed record AssuranceSemanticInvariantValidationResult (IReadOnlyList<AssuranceSemanticInvariantViolation> Violations)
{
    /// <summary> Gets a value indicating whether no semantic invariant violations were found. </summary>
    public bool IsValid => Violations.Count == 0;
}
