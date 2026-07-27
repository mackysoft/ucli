using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

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
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }
            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            var resultIndexByStepId = new Dictionary<IpcExecuteStepId, int>();
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                resultIndexByStepId.Add(steps[stepIndex].Id, stepIndex);
            }

            var violations = new List<IpcExecuteContractViolation>();
            for (var traceIndex = 0; traceIndex < operationTraces.Count; traceIndex++)
            {
                var trace = operationTraces[traceIndex];
                var contracts = trace.Contracts;
                if (contracts == null)
                {
                    continue;
                }
                if (!resultIndexByStepId.TryGetValue(trace.OpId, out var resultIndex))
                {
                    throw new InvalidOperationException("Operation trace does not correspond to a normalized request step.");
                }

                if (trace.Changed && !contracts.MayDirty && !contracts.MayPersist)
                {
                    AddContractViolation(
                        violations,
                        trace,
                        resultIndex,
                        expectedFact: "assurance.mayDirty=false",
                        observedResult: "opResults[].changed=true");
                }

                if (trace.Persisted && !contracts.MayPersist)
                {
                    AddContractViolation(
                        violations,
                        trace,
                        resultIndex,
                        expectedFact: "assurance.mayPersist=false",
                        observedResult: "executionTrace.persisted=true");
                }

                AddTouchedKindViolations(violations, trace, resultIndex, contracts);
                AddQueryKindViolations(violations, trace, resultIndex, contracts);
            }

            return violations.ToArray();
        }

        private static void AddTouchedKindViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationPhaseTrace.ContractFacts contracts)
        {
            var allowedTouchedKinds = new HashSet<UcliTouchedResourceKind>(contracts.TouchedKinds);
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
                    expectedFact: "assurance.touchedKinds=[" + string.Join(",", contracts.TouchedKinds.Select(static kind => TextVocabulary.GetText(kind))) + "]",
                    observedResult: "opResults[].touched[].kind=" + TextVocabulary.GetText(touchedKind));
            }
        }

        private static void AddQueryKindViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            int resultIndex,
            OperationPhaseTrace.ContractFacts contracts)
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

        private static IpcApplicationState ResolveApplicationState (OperationPhaseTrace trace)
        {
            if (trace.Persisted || trace.Applied)
            {
                return IpcApplicationState.Applied;
            }

            if (trace.Changed)
            {
                return IpcApplicationState.Indeterminate;
            }

            return IpcApplicationState.NotApplied;
        }
    }
}
