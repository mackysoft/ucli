using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one final per-operation trace entry produced by phase execution. </summary>
    internal sealed partial record OperationPhaseTrace
    {
        private readonly OperationPhaseStepResult outcome;

        private OperationPhaseTrace (
            IpcExecuteStepId opId,
            string op,
            OperationPhase phase,
            OperationPhaseStepResult outcome,
            OperationContractFacts? contracts)
        {
            OpId = opId ?? throw new ArgumentNullException(nameof(opId));
            if (string.IsNullOrWhiteSpace(op))
            {
                throw new ArgumentException("Operation name must not be empty.", nameof(op));
            }

            this.outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            if (outcome.TypedResult != null)
            {
                throw new ArgumentException("A trace requires a finalized serialized step result.", nameof(outcome));
            }

            Op = op;
            Phase = phase;
            Contracts = contracts;
        }

        public IpcExecuteStepId OpId { get; }

        public string Op { get; }

        public OperationPhase Phase { get; }

        public bool Applied => outcome.Applied;

        public bool Changed => outcome.Changed;

        public IReadOnlyList<OperationTouch> Touched => outcome.Touched;

        public OperationFailure? Failure => outcome.Failure;

        public JsonElement? Result => outcome.Result;

        public Verdict? Verdict => outcome.Verdict;

        public IReadOnlyList<OperationReadInvalidation> ReadInvalidations => outcome.ReadInvalidations;

        public IReadOnlyList<OperationDiagnostic> Diagnostics => outcome.Diagnostics;

        public bool Persisted => outcome.Persisted;

        public OperationContractFacts? Contracts { get; }

        public OperationPhaseTrace WithFailure (OperationFailure failure)
        {
            if (Verdict.HasValue)
            {
                throw new InvalidOperationException(
                    "A judging success trace cannot be converted into a failure trace.");
            }

            return new OperationPhaseTrace(OpId, Op, Phase, outcome.ReplaceFailure(failure), Contracts);
        }
    }
}
