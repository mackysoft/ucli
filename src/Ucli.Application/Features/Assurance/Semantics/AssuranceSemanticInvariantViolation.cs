namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Represents one semantic invariant violation in an assurance payload. </summary>
/// <param name="Path"> The JSON path of the violating value. </param>
/// <param name="Message"> The invariant violation message. </param>
internal sealed record AssuranceSemanticInvariantViolation (
    string Path,
    string Message);
