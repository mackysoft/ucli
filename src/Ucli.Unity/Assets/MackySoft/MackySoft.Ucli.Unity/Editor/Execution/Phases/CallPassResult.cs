using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one call-pass result. </summary>
    /// <param name="OperationTraces"> The per-operation traces from call pass. </param>
    /// <param name="Errors"> The call-pass errors. </param>
    internal sealed record CallPassResult (
        IReadOnlyList<OperationPhaseTrace> OperationTraces,
        IReadOnlyList<OperationFailure> Errors)
    {
        /// <summary> Gets a value indicating whether call pass succeeded. </summary>
        public bool IsSuccess => Errors.Count == 0;
    }
}