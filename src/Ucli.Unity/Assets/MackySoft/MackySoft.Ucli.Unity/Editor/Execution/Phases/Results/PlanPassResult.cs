using System;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one validate/plan pass result. </summary>
    /// <param name="CompiledSteps"> The public steps compiled against the current execution state. </param>
    /// <param name="CompiledDigestPayloadUtf8"> The canonical UTF-8 payload for the actual compiled primitive execution. </param>
    /// <param name="OperationTraces"> The per-operation traces from validate/plan pass. </param>
    /// <param name="Errors"> The validate/plan pass errors. </param>
    /// <param name="PreparedOperations"> The operations prepared for call-phase execution. </param>
    internal sealed record PlanPassResult (
        IReadOnlyList<NormalizedRequestStep> CompiledSteps,
        ReadOnlyMemory<byte> CompiledDigestPayloadUtf8,
        IReadOnlyList<OperationPhaseTrace> OperationTraces,
        IReadOnlyList<OperationFailure> Errors,
        IReadOnlyList<PreparedOperation> PreparedOperations)
    {
        /// <summary> Gets a value indicating whether validate/plan pass succeeded. </summary>
        public bool IsSuccess => Errors.Count == 0;
    }
}
