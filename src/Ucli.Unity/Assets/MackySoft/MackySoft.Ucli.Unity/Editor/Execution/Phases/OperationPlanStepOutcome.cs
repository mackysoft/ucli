#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation result from validate/plan step execution. </summary>
    /// <param name="OperationTrace"> The trace generated for the operation. </param>
    /// <param name="Error"> The failure recorded for fail-fast handling; otherwise <see langword="null" />. </param>
    /// <param name="PreparedOperation"> The prepared operation for call-pass when successful; otherwise <see langword="null" />. </param>
    internal sealed record OperationPlanStepOutcome (
        OperationPhaseTrace OperationTrace,
        OperationFailure? Error,
        PreparedOperation? PreparedOperation);
}
