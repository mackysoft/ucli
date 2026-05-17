using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Builds execute-dispatch response envelopes from internal execution models. </summary>
    internal static class ExecuteResponseBuilder
    {
        private const string ContractViolationMessage = "Operation result violated declared assurance facts.";

        /// <summary> Creates one execution response from phase execution trace. </summary>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="trace"> The phase execution trace. </param>
        /// <param name="serializerOptions"> The serializer options for payload conversion. </param>
        /// <returns> The mapped execution response. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any reference argument is <see langword="null" />. </exception>
        public static IpcResponse CreateExecutionResponse (
            ExecuteDispatchContext context,
            PhaseExecutionTrace trace,
            JsonSerializerOptions serializerOptions)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (serializerOptions == null)
            {
                throw new ArgumentNullException(nameof(serializerOptions));
            }

            var issuedAtUtc = DateTimeOffset.UtcNow;
            var contractViolations = CreateContractViolations(trace.OperationTraces);
            var payloadModel = CreateExecutePayload(context.Project, trace.Steps, trace.OperationTraces, trace.PlanToken, issuedAtUtc, contractViolations);
            var errors = CreateErrors(trace.Errors, contractViolations);
            return new IpcResponse(
                ProtocolVersion: context.ProtocolVersion,
                RequestId: context.RequestId,
                Status: errors.Length == 0 ? IpcProtocol.StatusOk : IpcProtocol.StatusError,
                Payload: JsonSerializer.SerializeToElement(payloadModel, serializerOptions),
                Errors: errors);
        }

        /// <summary> Creates an error response with one error entry. </summary>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="code"> The error code. </param>
        /// <param name="message"> The error message. </param>
        /// <param name="opId"> The related operation identifier. </param>
        /// <param name="serializerOptions"> The serializer options for payload conversion. </param>
        /// <returns> The error response envelope. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> or <paramref name="serializerOptions" /> is <see langword="null" />. </exception>
        public static IpcResponse CreateErrorResponse (
            ExecuteDispatchContext context,
            UcliCode code,
            string message,
            string? opId,
            JsonSerializerOptions serializerOptions)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (serializerOptions == null)
            {
                throw new ArgumentNullException(nameof(serializerOptions));
            }

            return new IpcResponse(
                ProtocolVersion: context.ProtocolVersion,
                RequestId: context.RequestId,
                Status: IpcProtocol.StatusError,
                Payload: JsonSerializer.SerializeToElement(CreateEmptyExecutePayload(context.Project), serializerOptions),
                Errors: new[]
                {
                    new IpcError(code, message, opId),
                });
        }

        /// <summary>
        /// Creates one execute payload from compiled step metadata and primitive traces.
        /// </summary>
        /// <param name="project"> The resolved project identity. </param>
        /// <param name="steps"> The normalized public steps in source order. Must not be <see langword="null" />. </param>
        /// <param name="operationTraces"> The primitive traces in compiled execution order. Must not be <see langword="null" />. </param>
        /// <param name="planToken"> The optional plan token issued for the response. </param>
        /// <param name="issuedAtUtc"> The timestamp used for mutation read-postcondition generation. </param>
        /// <returns> The execute payload whose <c>opResults</c> are aggregated back to public step granularity. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="steps" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        private static IpcExecuteResponse CreateExecutePayload (
            IpcProjectIdentity project,
            IReadOnlyList<Execution.Requests.NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            string? planToken,
            DateTimeOffset issuedAtUtc,
            IReadOnlyList<IpcExecuteContractViolation> contractViolations)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            if (contractViolations == null)
            {
                throw new ArgumentNullException(nameof(contractViolations));
            }

            var opResults = new IpcExecuteOperationResult[steps.Count];
            var operationTraceIndex = 0;
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var step = steps[stepIndex];
                if (step.PrimitiveCount == 0)
                {
                    opResults[stepIndex] = IpcExecuteOperationResultFactory.CreatePlanResult(
                        opId: step.Id,
                        op: step.OperationName,
                        applied: false,
                        changed: false,
                        touched: Array.Empty<IpcExecuteTouchedResource>(),
                        diagnostics: MapDiagnostics(step.Diagnostics));
                    continue;
                }

                if (operationTraceIndex + step.PrimitiveCount > operationTraces.Count)
                {
                    throw new InvalidOperationException("Operation traces do not match compiled step metadata.");
                }

                var lastPhase = OperationPhase.Skipped;
                var applied = false;
                var changed = false;
                JsonElement? result = null;
                var touchedResources = AggregateTouched(step.PrimitiveCount, operationTraces, operationTraceIndex, ref lastPhase, ref applied, ref changed, ref result);
                var diagnostics = AggregateDiagnostics(step.Diagnostics, step.PrimitiveCount, operationTraces, operationTraceIndex);

                opResults[stepIndex] = IpcExecuteOperationResultFactory.Create(
                    opId: step.Id,
                    op: step.OperationName,
                    phase: ToOperationPhaseName(lastPhase),
                    applied: applied,
                    changed: changed,
                    touched: touchedResources,
                    result: step.Kind == IpcRequestStepKind.Op ? result : null,
                    diagnostics: diagnostics);
                operationTraceIndex += step.PrimitiveCount;
            }

            return new IpcExecuteResponse(opResults)
            {
                Project = project,
                PlanToken = planToken,
                ReadPostcondition = CreateReadPostcondition(operationTraces, issuedAtUtc),
                ContractViolations = contractViolations.Count == 0 ? null : contractViolations,
            };
        }

        private static IpcExecuteContractViolation[] CreateContractViolations (
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
                var touchedKind = ToTouchedResourceKindName(trace.Touched[touchIndex].Kind);
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
                ApplicationState: IpcExecuteApplicationStateNames.Indeterminate));
        }

        /// <summary>
        /// Aggregates touched resources and result flags across one compiled primitive range.
        /// </summary>
        /// <param name="primitiveCount"> The number of primitive traces that belong to the current public step. </param>
        /// <param name="operationTraces"> The primitive traces in compiled execution order. </param>
        /// <param name="startIndex"> The first primitive index that belongs to the current public step. </param>
        /// <param name="lastPhase"> Receives the aggregated public phase for the compiled primitive range. Trailing skipped primitives do not overwrite an earlier non-skipped phase. </param>
        /// <param name="applied"> Receives <see langword="true" /> when any primitive in the aggregated range was applied. </param>
        /// <param name="changed"> Receives <see langword="true" /> when any primitive in the aggregated range changed state. </param>
        /// <param name="result"> Receives the last primitive result in the aggregated range. </param>
        /// <returns> The touched resources in first-seen order with duplicates removed by kind, path, and GUID. </returns>
        private static IpcExecuteTouchedResource[] AggregateTouched (
            int primitiveCount,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            int startIndex,
            ref OperationPhase lastPhase,
            ref bool applied,
            ref bool changed,
            ref JsonElement? result)
        {
            var touchedResources = new List<IpcExecuteTouchedResource>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < primitiveCount; i++)
            {
                var operationTrace = operationTraces[startIndex + i];
                if (operationTrace.Phase != OperationPhase.Skipped
                    || lastPhase == OperationPhase.Skipped)
                {
                    lastPhase = operationTrace.Phase;
                }

                applied |= operationTrace.Applied;
                changed |= operationTrace.Changed;
                result = operationTrace.Result;
                for (var touchedIndex = 0; touchedIndex < operationTrace.Touched.Count; touchedIndex++)
                {
                    var touchedResource = operationTrace.Touched[touchedIndex];
                    var key = touchedResource.Kind + "\u001f" + touchedResource.Path + "\u001f" + touchedResource.Guid;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    touchedResources.Add(new IpcExecuteTouchedResource(
                        Kind: ToTouchedResourceKindName(touchedResource.Kind),
                        Path: touchedResource.Path,
                        Guid: touchedResource.Guid));
                }
            }

            return touchedResources.ToArray();
        }

        private static IpcExecuteDiagnostic[] AggregateDiagnostics (
            IReadOnlyList<OperationDiagnostic> stepDiagnostics,
            int primitiveCount,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            int startIndex)
        {
            var diagnostics = new List<IpcExecuteDiagnostic>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            AddDiagnostics(stepDiagnostics, diagnostics, seen);
            for (var i = 0; i < primitiveCount; i++)
            {
                AddDiagnostics(operationTraces[startIndex + i].Diagnostics, diagnostics, seen);
            }

            return diagnostics.ToArray();
        }

        private static void AddDiagnostics (
            IReadOnlyList<OperationDiagnostic> source,
            List<IpcExecuteDiagnostic> diagnostics,
            HashSet<string> seen)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var diagnostic = source[i];
                var key = diagnostic.Code.Value + "\u001f" + diagnostic.Severity + "\u001f" + diagnostic.CoverageImpact + "\u001f" + diagnostic.Message;
                if (!seen.Add(key))
                {
                    continue;
                }

                diagnostics.Add(MapDiagnostic(diagnostic));
            }
        }

        private static IpcExecuteDiagnostic[] MapDiagnostics (IReadOnlyList<OperationDiagnostic> source)
        {
            var diagnostics = new IpcExecuteDiagnostic[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                diagnostics[i] = MapDiagnostic(source[i]);
            }

            return diagnostics;
        }

        private static IpcExecuteDiagnostic MapDiagnostic (OperationDiagnostic diagnostic)
        {
            return new IpcExecuteDiagnostic(
                Code: diagnostic.Code,
                Severity: diagnostic.Severity,
                CoverageImpact: diagnostic.CoverageImpact,
                Message: diagnostic.Message);
        }

        /// <summary> Creates one empty execute payload. </summary>
        /// <returns> The empty execute payload contract model. </returns>
        private static IpcExecuteResponse CreateEmptyExecutePayload (IpcProjectIdentity project)
        {
            return new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>())
            {
                Project = project,
            };
        }

        private static IpcExecuteReadPostcondition? CreateReadPostcondition (
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            DateTimeOffset issuedAtUtc)
        {
            var requirements = new List<IpcExecuteReadPostconditionRequirement>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var traceIndex = 0; traceIndex < operationTraces.Count; traceIndex++)
            {
                var operationTrace = operationTraces[traceIndex];
                for (var invalidationIndex = 0; invalidationIndex < operationTrace.ReadInvalidations.Count; invalidationIndex++)
                {
                    var invalidation = operationTrace.ReadInvalidations[invalidationIndex];
                    var surfaceName = ToReadPostconditionSurfaceName(invalidation.Surface);
                    var normalizedScenePath = invalidation.ScenePath == null
                        ? null
                        : PathStringNormalizer.ToSlashSeparated(invalidation.ScenePath);
                    var key = surfaceName + "\u001f" + normalizedScenePath;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    requirements.Add(new IpcExecuteReadPostconditionRequirement(
                        Surface: surfaceName,
                        MinSafeGeneratedAtUtc: issuedAtUtc)
                    {
                        ScenePath = normalizedScenePath,
                    });
                }
            }

            return requirements.Count == 0
                ? null
                : new IpcExecuteReadPostcondition(requirements.ToArray());
        }

        /// <summary> Creates IPC errors from operation failures. </summary>
        /// <param name="failures"> The operation failures to map. </param>
        /// <returns> The mapped IPC errors. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failures" /> is <see langword="null" />. </exception>
        private static IpcError[] CreateErrors (
            IReadOnlyList<OperationFailure> failures,
            IReadOnlyList<IpcExecuteContractViolation> contractViolations)
        {
            if (failures == null)
            {
                throw new ArgumentNullException(nameof(failures));
            }

            if (contractViolations == null)
            {
                throw new ArgumentNullException(nameof(contractViolations));
            }

            var violationErrorCount = CountUniqueViolationOperations(contractViolations);
            var errors = new IpcError[failures.Count + violationErrorCount];
            for (var i = 0; i < failures.Count; i++)
            {
                var failure = failures[i];
                errors[i] = new IpcError(failure.Code, failure.Message, failure.OpId);
            }

            var errorIndex = failures.Count;
            var seenViolationOpIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < contractViolations.Count; i++)
            {
                var violation = contractViolations[i];
                if (!seenViolationOpIds.Add(violation.OpId))
                {
                    continue;
                }

                errors[errorIndex] = new IpcError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    violation.OpId);
                errorIndex++;
            }

            return errors;
        }

        private static int CountUniqueViolationOperations (IReadOnlyList<IpcExecuteContractViolation> contractViolations)
        {
            var opIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < contractViolations.Count; i++)
            {
                opIds.Add(contractViolations[i].OpId);
            }

            return opIds.Count;
        }

        /// <summary> Converts one operation phase to protocol literal. </summary>
        /// <param name="phase"> The operation phase. </param>
        /// <returns> The protocol phase literal. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when phase has unsupported value. </exception>
        private static string ToOperationPhaseName (OperationPhase phase)
        {
            switch (phase)
            {
                case OperationPhase.Validate:
                    return IpcExecuteOperationPhaseNames.Validate;

                case OperationPhase.Plan:
                    return IpcExecuteOperationPhaseNames.Plan;

                case OperationPhase.Call:
                    return IpcExecuteOperationPhaseNames.Call;

                case OperationPhase.Skipped:
                    return IpcExecuteOperationPhaseNames.Skipped;

                default:
                    throw new InvalidOperationException($"Unsupported operation phase '{phase}'.");
            }
        }

        /// <summary> Converts one touched resource kind to protocol literal. </summary>
        /// <param name="kind"> The touched resource kind. </param>
        /// <returns> The protocol touched kind literal. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when kind has unsupported value. </exception>
        private static string ToTouchedResourceKindName (OperationTouchKind kind)
        {
            switch (kind)
            {
                case OperationTouchKind.Scene:
                    return IpcExecuteTouchedResourceKindNames.Scene;

                case OperationTouchKind.Prefab:
                    return IpcExecuteTouchedResourceKindNames.Prefab;

                case OperationTouchKind.Asset:
                    return IpcExecuteTouchedResourceKindNames.Asset;

                case OperationTouchKind.ProjectSettings:
                    return IpcExecuteTouchedResourceKindNames.ProjectSettings;

                default:
                    throw new InvalidOperationException($"Unsupported touched resource kind '{kind}'.");
            }
        }

        private static string ToReadPostconditionSurfaceName (OperationReadInvalidationSurface surface)
        {
            switch (surface)
            {
                case OperationReadInvalidationSurface.AssetSearch:
                    return IpcExecuteReadPostconditionSurfaceNames.AssetSearch;

                case OperationReadInvalidationSurface.GuidPath:
                    return IpcExecuteReadPostconditionSurfaceNames.GuidPath;

                case OperationReadInvalidationSurface.SceneTreeLite:
                    return IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite;

                default:
                    throw new InvalidOperationException($"Unsupported read invalidation surface '{surface}'.");
            }
        }
    }
}
