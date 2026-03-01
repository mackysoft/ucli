using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one validate/plan pass result. </summary>
    /// <param name="OperationTraces"> The per-operation traces from validate/plan pass. </param>
    /// <param name="Errors"> The validate/plan pass errors. </param>
    /// <param name="PreparedOperations"> The operations prepared for call-phase execution. </param>
    internal sealed record PlanPassResult (
        IReadOnlyList<OperationPhaseTrace> OperationTraces,
        IReadOnlyList<OperationFailure> Errors,
        IReadOnlyList<PreparedOperation> PreparedOperations)
    {
        /// <summary> Gets a value indicating whether validate/plan pass succeeded. </summary>
        public bool IsSuccess => Errors.Count == 0;
    }
}
