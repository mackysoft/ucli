using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Detects runtime result contradictions against declared operation assurance facts. </summary>
    internal static class OperationContractViolationDetector
    {
        /// <summary> Detects contract violations from operation execution traces. </summary>
        /// <param name="operationTraces"> The operation traces to inspect. </param>
        /// <returns> The detected contract violations in trace order. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        public static IpcExecuteContractViolation[] Detect (
            IReadOnlyList<OperationPhaseTrace> operationTraces)
        {
            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
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

                if (trace.Changed && !contracts.MayDirty)
                {
                    AddContractViolation(
                        violations,
                        trace,
                        expectedFact: "assurance.mayDirty=false",
                        observedResult: "opResults[].changed=true");
                }

                if (trace.Persisted && !contracts.MayPersist)
                {
                    AddContractViolation(
                        violations,
                        trace,
                        expectedFact: "assurance.mayPersist=false",
                        observedResult: "executionTrace.persisted=true");
                }

                AddTouchedKindViolations(violations, trace, contracts);
                AddQueryKindViolations(violations, trace, contracts);
            }

            return violations.ToArray();
        }

        private static void AddTouchedKindViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            OperationPhaseTrace.ContractFacts contracts)
        {
            var allowedTouchedKinds = new HashSet<string>(contracts.TouchedKinds, StringComparer.Ordinal);
            for (var touchIndex = 0; touchIndex < trace.Touched.Count; touchIndex++)
            {
                var touchedKind = IpcExecuteTouchedResourceKindMapper.ToName(trace.Touched[touchIndex].Kind);
                if (allowedTouchedKinds.Contains(touchedKind))
                {
                    continue;
                }

                AddContractViolation(
                    violations,
                    trace,
                    expectedFact: "assurance.touchedKinds=[" + string.Join(",", contracts.TouchedKinds) + "]",
                    observedResult: "opResults[].touched[].kind=" + touchedKind);
            }
        }

        private static void AddQueryKindViolations (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
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
                    expectedFact: "operation.kind=query",
                    observedResult: "opResults[].applied=true");
            }

            if (trace.Changed)
            {
                AddContractViolation(
                    violations,
                    trace,
                    expectedFact: "operation.kind=query",
                    observedResult: "opResults[].changed=true");
            }

            if (trace.Touched.Count != 0)
            {
                AddContractViolation(
                    violations,
                    trace,
                    expectedFact: "operation.kind=query",
                    observedResult: "opResults[].touched.length=" + trace.Touched.Count);
            }
        }

        private static void AddContractViolation (
            List<IpcExecuteContractViolation> violations,
            OperationPhaseTrace trace,
            string expectedFact,
            string observedResult)
        {
            violations.Add(new IpcExecuteContractViolation(
                OpId: trace.OpId,
                Operation: trace.Op,
                ExpectedFact: expectedFact,
                ObservedResult: observedResult,
                ApplicationState: ResolveApplicationState(trace)));
        }

        private static string ResolveApplicationState (OperationPhaseTrace trace)
        {
            if (trace.Persisted || trace.Applied)
            {
                return IpcExecuteApplicationStateNames.Applied;
            }

            if (trace.Changed)
            {
                return IpcExecuteApplicationStateNames.Indeterminate;
            }

            return IpcExecuteApplicationStateNames.NotApplied;
        }
    }
}
