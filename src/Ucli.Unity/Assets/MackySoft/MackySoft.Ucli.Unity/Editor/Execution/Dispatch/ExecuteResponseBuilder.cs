using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Builds execute-dispatch response envelopes from internal execution models. </summary>
    internal static class ExecuteResponseBuilder
    {
        private const string ContractViolationMessage = "Operation result violated declared assurance facts.";

        /// <summary> Creates one execution response from phase execution trace. </summary>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="executedPass"> The plan or call pass that produced the trace. </param>
        /// <param name="trace"> The phase execution trace. </param>
        /// <returns> The mapped execution response. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any reference argument is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="executedPass" /> is not plan or call. </exception>
        public static IpcResponse CreateExecutionResponse (
            ExecuteDispatchContext context,
            OperationPhase executedPass,
            PhaseExecutionTrace trace)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            ThrowIfUnsupportedExecutedPass(executedPass);
            var issuedAtUtc = DateTimeOffset.UtcNow;
            var contractViolations = OperationContractViolationDetector.Detect(trace.Steps, trace.OperationTraces);
            var payloadModel = CreateExecutePayload(
                context.Project,
                trace.Steps,
                trace.OperationTraces,
                executedPass,
                trace.PlanToken,
                issuedAtUtc,
                contractViolations,
                trace.IsSuccess);
            var errors = CreateErrors(trace.Steps, trace.Errors, contractViolations);
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: context.RequestId,
                status: errors.Length == 0 ? IpcResponseStatus.Ok : IpcResponseStatus.Error,
                payload: IpcPayloadCodec.SerializeToElement(payloadModel),
                errors: errors);
        }

        /// <summary> Creates an error response with one error entry. </summary>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="code"> The error code. </param>
        /// <param name="message"> The error message. </param>
        /// <param name="instancePath"> The RFC 6901 path of the related value when available. </param>
        /// <returns> The error response envelope. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
        public static IpcResponse CreateErrorResponse (
            ExecuteDispatchContext context,
            UcliCode code,
            string message,
            string? instancePath)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: context.RequestId,
                status: IpcResponseStatus.Error,
                payload: IpcPayloadCodec.SerializeToElement(CreateEmptyExecutePayload(context.Project)),
                errors: new[]
                {
                    new IpcError(code, message, instancePath),
                });
        }

        /// <summary>
        /// Creates one execute payload from compiled step metadata and primitive traces.
        /// </summary>
        /// <param name="project"> The resolved project identity. </param>
        /// <param name="steps"> The normalized public steps in source order. Must not be <see langword="null" />. </param>
        /// <param name="operationTraces"> The primitive traces in compiled execution order. Must not be <see langword="null" />. </param>
        /// <param name="executedPass"> The plan or call pass that produced the primitive traces. </param>
        /// <param name="planToken"> The optional plan token issued for the response. </param>
        /// <param name="issuedAtUtc"> The timestamp used for mutation read-postcondition generation. </param>
        /// <returns> The execute payload whose <c>opResults</c> are aggregated back to public step granularity. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="steps" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        private static IpcExecuteResponse CreateExecutePayload (
            UnityProjectIdentity project,
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            OperationPhase executedPass,
            string? planToken,
            DateTimeOffset issuedAtUtc,
            IReadOnlyList<IpcExecuteContractViolation> contractViolations,
            bool executionSucceeded)
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

            if (HasUnresolvedDirectStep(steps))
            {
                if (executionSucceeded)
                {
                    throw new InvalidOperationException(
                        "A successful direct-operation response must contain its fixed descriptor digest.");
                }

                return new IpcExecuteResponse(
                    Array.Empty<IpcExecuteOperationResult>(),
                    project,
                    planToken,
                    CreateReadPostcondition(operationTraces, issuedAtUtc),
                    CreatePostReadSource(steps),
                    contractViolations.Count == 0 ? null : contractViolations);
            }

            var opResults = new IpcExecuteOperationResult[steps.Count];
            var operationTraceIndex = 0;
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var step = steps[stepIndex];
                if (step.PrimitiveCount == 0)
                {
                    var stepDiagnostics = MapDiagnostics(step.Diagnostics);
                    if (step.Kind == IpcExecuteStepKind.Op)
                    {
                        var directOperationDescriptorDigest = step.OperationDescriptorDigest
                            ?? throw new InvalidOperationException(
                                "A direct-operation result requires the descriptor fixed before execution.");
                        opResults[stepIndex] = IpcExecuteOperationResultFactory.CreateDirectWithoutVerdict(
                            op: step.OperationName,
                            phase: IpcExecuteOperationPhase.Skipped,
                            applied: false,
                            changed: false,
                            touched: Array.Empty<IpcExecuteTouchedResource>(),
                            operationDescriptorDigest: directOperationDescriptorDigest,
                            result: null,
                            diagnostics: stepDiagnostics);
                    }
                    else if (step.Kind == IpcExecuteStepKind.Edit)
                    {
                        opResults[stepIndex] = IpcExecuteOperationResultFactory.CreateEditResult(
                            phase: MapOperationPhase(executedPass),
                            applied: false,
                            changed: false,
                            touched: Array.Empty<IpcExecuteTouchedResource>(),
                            diagnostics: stepDiagnostics);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "A compiled step must be a direct Operation or Edit.");
                    }
                    continue;
                }

                if (operationTraceIndex + step.PrimitiveCount > operationTraces.Count)
                {
                    throw new InvalidOperationException("Operation traces do not match compiled step metadata.");
                }

                var aggregate = AggregateOperationTraces(
                    step.PrimitiveCount,
                    operationTraces,
                    operationTraceIndex);
                var diagnostics = AggregateDiagnostics(step.Diagnostics, step.PrimitiveCount, operationTraces, operationTraceIndex);
                var isDirectOperation = step.Kind == IpcExecuteStepKind.Op;
                if (isDirectOperation
                    && aggregate.OperationDescriptorDigest != null
                    && aggregate.OperationDescriptorDigest != step.OperationDescriptorDigest)
                {
                    throw new InvalidOperationException(
                        "Operation trace descriptor digest does not match the fixed public-step descriptor.");
                }

                var operationDescriptorDigest = isDirectOperation
                    ? step.OperationDescriptorDigest
                    : null;
                var emittedVerdict = isDirectOperation
                    && !HasContractViolation(contractViolations, stepIndex)
                        ? aggregate.Verdict
                        : null;
                if (emittedVerdict.HasValue)
                {
                    if (aggregate.LastPhase != OperationPhase.Call
                        || operationDescriptorDigest == null
                        || !aggregate.Result.HasValue)
                    {
                        throw new InvalidOperationException(
                            "A judging operation trace must contain its Call descriptor and result evidence.");
                    }

                    opResults[stepIndex] = IpcExecuteOperationResultFactory.CreateJudgingCallResult(
                        op: step.OperationName,
                        applied: aggregate.Applied,
                        changed: aggregate.Changed,
                        touched: aggregate.TouchedResources,
                        operationDescriptorDigest: operationDescriptorDigest,
                        verdict: emittedVerdict.Value,
                        result: aggregate.Result.Value,
                        diagnostics: diagnostics);
                }
                else
                {
                    if (isDirectOperation)
                    {
                        var directOperationDescriptorDigest = operationDescriptorDigest
                            ?? throw new InvalidOperationException(
                                "A direct-operation result requires the descriptor fixed before execution.");
                        opResults[stepIndex] = IpcExecuteOperationResultFactory.CreateDirectWithoutVerdict(
                            op: step.OperationName,
                            phase: MapOperationPhase(aggregate.LastPhase),
                            applied: aggregate.Applied,
                            changed: aggregate.Changed,
                            touched: aggregate.TouchedResources,
                            operationDescriptorDigest: directOperationDescriptorDigest,
                            result: aggregate.Result,
                            diagnostics: diagnostics);
                    }
                    else if (step.Kind == IpcExecuteStepKind.Edit)
                    {
                        opResults[stepIndex] = IpcExecuteOperationResultFactory.CreateEditResult(
                            phase: MapOperationPhase(aggregate.LastPhase),
                            applied: aggregate.Applied,
                            changed: aggregate.Changed,
                            touched: aggregate.TouchedResources,
                            diagnostics: diagnostics);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "A compiled step must be a direct Operation or Edit.");
                    }
                }
                operationTraceIndex += step.PrimitiveCount;
            }

            EnsureAllOperationTracesConsumed(operationTraceIndex, operationTraces.Count);

            return new IpcExecuteResponse(
                opResults,
                project,
                planToken,
                CreateReadPostcondition(operationTraces, issuedAtUtc),
                CreatePostReadSource(steps),
                contractViolations.Count == 0 ? null : contractViolations);
        }

        private static bool HasUnresolvedDirectStep (IReadOnlyList<NormalizedRequestStep> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Kind == IpcExecuteStepKind.Op
                    && steps[i].OperationDescriptorDigest == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ThrowIfUnsupportedExecutedPass (OperationPhase executedPass)
        {
            if (executedPass is not OperationPhase.Plan and not OperationPhase.Call)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(executedPass),
                    executedPass,
                    "Only plan and call passes can produce an execution response.");
            }
        }

        private static void EnsureAllOperationTracesConsumed (
            int consumedTraceCount,
            int availableTraceCount)
        {
            if (consumedTraceCount != availableTraceCount)
            {
                throw new InvalidOperationException("Operation traces do not match compiled step metadata.");
            }
        }

        private static IpcExecutePostReadSource? CreatePostReadSource (IReadOnlyList<NormalizedRequestStep> steps)
        {
            var sourceSteps = new List<IpcExecutePostReadSourceStep>(steps.Count);
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var sourceStep = steps[stepIndex].PostReadSourceStep;
                if (sourceStep == null)
                {
                    return null;
                }

                sourceSteps.Add(sourceStep);
            }

            return new IpcExecutePostReadSource(IpcExecutePostReadSource.CurrentSchemaVersion, sourceSteps.ToArray());
        }

        /// <summary>
        /// Aggregates the execution state across one compiled primitive range.
        /// </summary>
        /// <param name="primitiveCount"> The number of primitive traces that belong to the current public step. </param>
        /// <param name="operationTraces"> The primitive traces in compiled execution order. </param>
        /// <param name="startIndex"> The first primitive index that belongs to the current public step. </param>
        /// <returns> One complete aggregate whose verdict state has already been validated. </returns>
        private static AggregatedOperationTraces AggregateOperationTraces (
            int primitiveCount,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            int startIndex)
        {
            if (primitiveCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(primitiveCount),
                    primitiveCount,
                    "An operation aggregate requires at least one primitive trace.");
            }

            var lastPhase = OperationPhase.Skipped;
            var applied = false;
            var changed = false;
            var touchedResources = new List<IpcExecuteTouchedResource>();
            var seen = new HashSet<OperationTouch>();
            OperationPhaseTrace? lastTrace = null;
            for (var i = 0; i < primitiveCount; i++)
            {
                var operationTrace = operationTraces[startIndex + i];
                lastTrace = operationTrace;
                if (operationTrace.Phase != OperationPhase.Skipped
                    || lastPhase == OperationPhase.Skipped)
                {
                    lastPhase = operationTrace.Phase;
                }

                applied |= operationTrace.Applied;
                changed |= operationTrace.Changed;
                for (var touchedIndex = 0; touchedIndex < operationTrace.Touched.Count; touchedIndex++)
                {
                    var touchedResource = operationTrace.Touched[touchedIndex];
                    if (!seen.Add(touchedResource))
                    {
                        continue;
                    }

                    touchedResources.Add(new IpcExecuteTouchedResource(
                        kind: touchedResource.Kind,
                        path: touchedResource.Path,
                        assetGuid: touchedResource.AssetGuid));
                }
            }

            return new AggregatedOperationTraces(
                lastPhase,
                applied,
                changed,
                touchedResources.ToArray(),
                lastTrace ?? throw new InvalidOperationException(
                    "An operation aggregate requires at least one primitive trace."));
        }

        private sealed class AggregatedOperationTraces
        {
            public AggregatedOperationTraces (
                OperationPhase lastPhase,
                bool applied,
                bool changed,
                IpcExecuteTouchedResource[] touchedResources,
                OperationPhaseTrace lastTrace)
            {
                if (lastTrace == null)
                {
                    throw new ArgumentNullException(nameof(lastTrace));
                }

                var requiresVerdict = lastTrace.Contracts is
                    {
                        HasVerdictContract: true,
                        OperationKind: UcliOperationKind.Query,
                    }
                    && lastTrace.Phase == OperationPhase.Call
                    && lastTrace.Failure == null
                    && lastTrace.Result.HasValue;
                if (lastTrace.Verdict.HasValue != requiresVerdict)
                {
                    throw new ArgumentException(
                        "A successful judging Query Call must contain exactly one verdict with its serialized result evidence.",
                        nameof(lastTrace));
                }

                LastPhase = lastPhase;
                Applied = applied;
                Changed = changed;
                TouchedResources = touchedResources
                    ?? throw new ArgumentNullException(nameof(touchedResources));
                Result = lastTrace.Result;
                OperationDescriptorDigest = lastTrace.Contracts?.DescriptorDigest;
                Verdict = lastTrace.Verdict;
            }

            public OperationPhase LastPhase { get; }

            public bool Applied { get; }

            public bool Changed { get; }

            public IpcExecuteTouchedResource[] TouchedResources { get; }

            public JsonElement? Result { get; }

            public Sha256Digest? OperationDescriptorDigest { get; }

            public Verdict? Verdict { get; }
        }

        private static bool HasContractViolation (
            IReadOnlyList<IpcExecuteContractViolation> contractViolations,
            int resultIndex)
        {
            var instancePath = "/opResults/" + resultIndex;
            for (var i = 0; i < contractViolations.Count; i++)
            {
                if (string.Equals(
                        contractViolations[i].InstancePath,
                        instancePath,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IpcExecuteDiagnostic[] AggregateDiagnostics (
            IReadOnlyList<OperationDiagnostic> stepDiagnostics,
            int primitiveCount,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            int startIndex)
        {
            var diagnostics = new List<IpcExecuteDiagnostic>();
            var seen = new HashSet<(string Code, UcliDiagnosticSeverity Severity, IpcExecuteDiagnosticCoverageImpact CoverageImpact, string Message)>();
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
            HashSet<(string Code, UcliDiagnosticSeverity Severity, IpcExecuteDiagnosticCoverageImpact CoverageImpact, string Message)> seen)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var diagnostic = source[i];
                var key = (diagnostic.Code.Value, diagnostic.Severity, diagnostic.CoverageImpact, diagnostic.Message);
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
        private static IpcExecuteResponse CreateEmptyExecutePayload (UnityProjectIdentity project)
        {
            return new IpcExecuteResponse(
                Array.Empty<IpcExecuteOperationResult>(),
                project,
                planToken: null,
                readPostcondition: null,
                postReadSource: null,
                contractViolations: null);
        }

        private static ExecutionReadPostcondition? CreateReadPostcondition (
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            DateTimeOffset issuedAtUtc)
        {
            var requirements = new List<ExecutionReadPostconditionRequirement>();
            var seen = new HashSet<(ExecutionReadPostconditionSurface Surface, UnityScenePath? ScenePath)>();
            for (var traceIndex = 0; traceIndex < operationTraces.Count; traceIndex++)
            {
                var operationTrace = operationTraces[traceIndex];
                for (var invalidationIndex = 0; invalidationIndex < operationTrace.ReadInvalidations.Count; invalidationIndex++)
                {
                    var invalidation = operationTrace.ReadInvalidations[invalidationIndex];
                    var surface = MapReadPostconditionSurface(invalidation.Surface);
                    var scenePath = invalidation.ScenePath == null
                        ? null
                        : new UnityScenePath(invalidation.ScenePath);
                    var key = (surface, scenePath);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    requirements.Add(new ExecutionReadPostconditionRequirement(
                        Surface: surface,
                        MinSafeGeneratedAtUtc: issuedAtUtc,
                        ScenePath: scenePath));
                }
            }

            return requirements.Count == 0
                ? null
                : new ExecutionReadPostcondition(requirements.ToArray());
        }

        /// <summary> Creates IPC errors from operation failures. </summary>
        /// <param name="failures"> The operation failures to map. </param>
        /// <returns> The mapped IPC errors. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failures" /> is <see langword="null" />. </exception>
        private static IpcError[] CreateErrors (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<OperationFailure> failures,
            IReadOnlyList<IpcExecuteContractViolation> contractViolations)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }
            if (failures == null)
            {
                throw new ArgumentNullException(nameof(failures));
            }

            if (contractViolations == null)
            {
                throw new ArgumentNullException(nameof(contractViolations));
            }

            var requestPathByStepId = new Dictionary<IpcExecuteStepId, string>();
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                requestPathByStepId.Add(steps[stepIndex].Id, "/steps/" + stepIndex);
            }

            var violationErrorCount = CountUniqueViolationOperations(contractViolations);
            var errors = new IpcError[failures.Count + violationErrorCount];
            for (var i = 0; i < failures.Count; i++)
            {
                var failure = failures[i];
                errors[i] = new IpcError(
                    failure.Code,
                    failure.Message,
                    failure.OpId == null ? null : requestPathByStepId[failure.OpId]);
            }

            var errorIndex = failures.Count;
            var seenViolationPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < contractViolations.Count; i++)
            {
                var violation = contractViolations[i];
                if (!seenViolationPaths.Add(violation.InstancePath))
                {
                    continue;
                }

                errors[errorIndex] = new IpcError(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    violation.InstancePath);
                errorIndex++;
            }

            return errors;
        }

        private static int CountUniqueViolationOperations (IReadOnlyList<IpcExecuteContractViolation> contractViolations)
        {
            var instancePaths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < contractViolations.Count; i++)
            {
                instancePaths.Add(contractViolations[i].InstancePath);
            }

            return instancePaths.Count;
        }

        /// <summary> Maps one internal operation phase to its IPC contract value. </summary>
        /// <param name="phase"> The operation phase. </param>
        /// <returns> The IPC operation phase. </returns>
        /// <exception cref="InvalidOperationException"> Thrown when phase has unsupported value. </exception>
        private static IpcExecuteOperationPhase MapOperationPhase (OperationPhase phase)
        {
            switch (phase)
            {
                case OperationPhase.Validate:
                    return IpcExecuteOperationPhase.Validate;

                case OperationPhase.Plan:
                    return IpcExecuteOperationPhase.Plan;

                case OperationPhase.Call:
                    return IpcExecuteOperationPhase.Call;

                case OperationPhase.Skipped:
                    return IpcExecuteOperationPhase.Skipped;

                default:
                    throw new InvalidOperationException($"Unsupported operation phase '{phase}'.");
            }
        }

        private static ExecutionReadPostconditionSurface MapReadPostconditionSurface (OperationReadInvalidationSurface surface)
        {
            switch (surface)
            {
                case OperationReadInvalidationSurface.AssetSearch:
                    return ExecutionReadPostconditionSurface.AssetSearch;

                case OperationReadInvalidationSurface.GuidPath:
                    return ExecutionReadPostconditionSurface.GuidPath;

                case OperationReadInvalidationSurface.SceneTreeLite:
                    return ExecutionReadPostconditionSurface.SceneTreeLite;

                default:
                    throw new InvalidOperationException($"Unsupported read invalidation surface '{surface}'.");
            }
        }
    }
}
