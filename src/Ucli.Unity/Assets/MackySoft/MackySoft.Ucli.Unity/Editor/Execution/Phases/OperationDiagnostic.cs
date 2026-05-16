using MackySoft.Ucli.Contracts;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one non-fatal diagnostic emitted by Unity-side operation execution. </summary>
    /// <param name="Code"> The stable diagnostic code. </param>
    /// <param name="Severity"> The diagnostic severity literal. </param>
    /// <param name="CoverageImpact"> The diagnostic coverage-impact literal. </param>
    /// <param name="Message"> The human-readable diagnostic message. </param>
    public sealed record OperationDiagnostic (
        UcliErrorCode Code,
        string Severity,
        string CoverageImpact,
        string Message);
}
