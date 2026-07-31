using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Detects runtime result contradictions against declared operation assurance facts. </summary>
    internal static class OperationContractViolationDetector
    {
        /// <summary> Detects contract violations from operation execution traces. </summary>
        /// <param name="steps"> The normalized public steps in source order. </param>
        /// <param name="operationTraces"> The operation traces to inspect. </param>
        /// <returns> The detected contract violations in trace order. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        public static IpcExecuteContractViolation[] Detect (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces)
        {
            _ = steps ?? throw new ArgumentNullException(nameof(steps));
            _ = operationTraces ?? throw new ArgumentNullException(nameof(operationTraces));

            var violations = new List<IpcExecuteContractViolation>();
            var traceIndex = 0;
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                traceIndex = AddStepViolations(
                    violations,
                    operationTraces,
                    steps[stepIndex],
                    stepIndex,
                    traceIndex);
            }

            RequireAllTracesConsumed(traceIndex, operationTraces.Count);
            return violations.ToArray();
        }

        private static int AddStepViolations (
            List<IpcExecuteContractViolation> violations,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            NormalizedRequestStep step,
            int resultIndex,
            int traceIndex)
        {
            var rangeEnd = RequireTraceRangeEnd(
                traceIndex,
                step.PrimitiveCount,
                operationTraces.Count);
            for (var currentTraceIndex = traceIndex; currentTraceIndex < rangeEnd; currentTraceIndex++)
            {
                AddMappedTraceViolations(
                    violations,
                    operationTraces[currentTraceIndex],
                    step,
                    resultIndex);
            }

            return rangeEnd;
        }

        private static int RequireTraceRangeEnd (
            int traceIndex,
            int primitiveCount,
            int traceCount)
        {
            var rangeEnd = traceIndex + primitiveCount;
            if (rangeEnd > traceCount)
            {
                throw new InvalidOperationException("Operation traces do not match compiled step metadata.");
            }

            return rangeEnd;
        }

        private static void AddMappedTraceViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            NormalizedRequestStep step,
            int resultIndex)
        {
            var contracts = trace.Contracts;
            if (contracts == null)
            {
                return;
            }
            if (step.Kind == IpcExecuteStepKind.Op && trace.OpId != step.Id)
            {
                throw new InvalidOperationException("Operation trace does not correspond to a normalized request step.");
            }

            AddTraceViolations(violations, trace, resultIndex, contracts);
        }

        private static void RequireAllTracesConsumed (
            int consumedTraceCount,
            int availableTraceCount)
        {
            if (consumedTraceCount != availableTraceCount)
            {
                throw new InvalidOperationException("Operation traces do not match compiled step metadata.");
            }
        }

        private static void AddTraceViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationContractFacts contracts)
        {
            if (contracts.OperationKind == UcliOperationKind.Query)
            {
                AddQueryKindViolations(violations, trace, resultIndex, contracts);
            }
            else
            {
                AddChangedViolation(violations, trace, resultIndex, contracts);
                AddTouchedKindViolations(violations, trace, resultIndex, contracts);
            }

            AddPersistenceViolation(violations, trace, resultIndex, contracts);
            AddVerdictViolations(violations, trace, resultIndex, contracts);
        }

        private static void AddChangedViolation (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationContractFacts contracts)
        {
            if (trace.Changed && !contracts.Assurance.MayDirty && !contracts.Assurance.MayPersist)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "assurance.mayDirty=false",
                    observedResult: "opResults[].changed=true");
            }
        }

        private static void AddPersistenceViolation (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationContractFacts contracts)
        {
            if (trace.Persisted && !contracts.Assurance.MayPersist)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "assurance.mayPersist=false",
                    observedResult: "executionTrace.persisted=true");
            }
        }

        private static void AddVerdictViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationContractFacts contracts)
        {
            var mayEmitVerdict = trace.Phase == OperationPhase.Call
                && trace.Failure == null
                && contracts.OperationKind == UcliOperationKind.Query
                && contracts.HasVerdictContract;

            if (mayEmitVerdict)
            {
                AddMissingVerdictEvidenceViolations(violations, trace, resultIndex);
                return;
            }

            if (trace.Verdict.HasValue)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "verdictContract=null or phase!=call",
                    observedResult: "opResults[].verdict="
                        + Vocabulary.GetText(trace.Verdict.Value));
            }
        }

        private static void AddMissingVerdictEvidenceViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex)
        {
            if (!trace.Result.HasValue)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "verdictContract requires a valid result",
                    observedResult: "opResults[].result=null");
            }

            if (!trace.Verdict.HasValue)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "verdictContract requires a Call verdict",
                    observedResult: "opResults[].verdict=null");
            }
        }

        private static void AddTouchedKindViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationContractFacts contracts)
        {
            var allowedTouchedKinds = new HashSet<UcliTouchedResourceKind>(contracts.Assurance.TouchedKinds);
            for (var touchIndex = 0; touchIndex < trace.Touched.Count; touchIndex++)
            {
                var touchedKind = trace.Touched[touchIndex].Kind;
                if (allowedTouchedKinds.Contains(touchedKind))
                {
                    continue;
                }

                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "assurance.touchedKinds=[" + string.Join(",", contracts.Assurance.TouchedKinds.Select(static kind => Vocabulary.GetText(kind))) + "]",
                    observedResult: "opResults[].touched[].kind=" + Vocabulary.GetText(touchedKind));
            }
        }

        private static void AddQueryKindViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationContractFacts contracts)
        {
            if (contracts.OperationKind != UcliOperationKind.Query)
            {
                return;
            }

            if (trace.Applied)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "operation.kind=query",
                    observedResult: "opResults[].applied=true");
            }

            if (trace.Changed)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "operation.kind=query",
                    observedResult: "opResults[].changed=true");
            }

            if (trace.Touched.Count != 0)
            {
                AddContractViolation(
                    violations,
                    trace,
                    resultIndex,
                    expectedFact: "operation.kind=query",
                    observedResult: "opResults[].touched.length=" + trace.Touched.Count);
            }
        }

        private static void AddContractViolation (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            string expectedFact,
            string observedResult)
        {
            violations.Add(new IpcExecuteContractViolation(
                InstancePath: "/opResults/" + resultIndex,
                Operation: trace.Op,
                ExpectedFact: expectedFact,
                ObservedResult: observedResult,
                ApplicationState: ResolveApplicationState(trace)));
        }

        private static ExecutionApplicationState ResolveApplicationState (OperationPhaseTrace trace)
        {
            if (trace.Persisted || trace.Applied)
            {
                return ExecutionApplicationState.Applied;
            }

            if (trace.Changed)
            {
                return ExecutionApplicationState.Indeterminate;
            }

            return ExecutionApplicationState.NotApplied;
        }
    }
}
