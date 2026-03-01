using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Builds execute-dispatch response envelopes from internal execution models. </summary>
    internal static class ExecuteResponseBuilder
    {
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

            var payloadModel = CreateExecutePayload(trace.OperationTraces, trace.PlanToken);
            var errors = CreateErrors(trace.Errors);
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
            string code,
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
                Payload: JsonSerializer.SerializeToElement(CreateEmptyExecutePayload(), serializerOptions),
                Errors: new[]
                {
                    new IpcError(code, message, opId),
                });
        }

        /// <summary> Creates one execute payload from operation traces. </summary>
        /// <param name="operationTraces"> The operation traces to map. </param>
        /// <returns> The execute payload contract model. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        private static IpcExecuteResponse CreateExecutePayload (
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            string? planToken)
        {
            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            var opResults = new IpcExecuteOperationResult[operationTraces.Count];
            for (var i = 0; i < operationTraces.Count; i++)
            {
                var operationTrace = operationTraces[i];
                var touchedResources = new IpcExecuteTouchedResource[operationTrace.Touched.Count];
                for (var touchedIndex = 0; touchedIndex < operationTrace.Touched.Count; touchedIndex++)
                {
                    var touchedResource = operationTrace.Touched[touchedIndex];
                    touchedResources[touchedIndex] = new IpcExecuteTouchedResource(
                        Kind: ToTouchedResourceKindName(touchedResource.Kind),
                        Path: touchedResource.Path,
                        Guid: touchedResource.Guid);
                }

                opResults[i] = new IpcExecuteOperationResult(
                    OpId: operationTrace.OpId,
                    Op: operationTrace.Op,
                    Phase: ToOperationPhaseName(operationTrace.Phase),
                    Applied: operationTrace.Applied,
                    Changed: operationTrace.Changed,
                    Touched: touchedResources);
            }

            return new IpcExecuteResponse(opResults)
            {
                PlanToken = planToken,
            };
        }

        /// <summary> Creates one empty execute payload. </summary>
        /// <returns> The empty execute payload contract model. </returns>
        private static IpcExecuteResponse CreateEmptyExecutePayload ()
        {
            return new IpcExecuteResponse(Array.Empty<IpcExecuteOperationResult>());
        }

        /// <summary> Creates IPC errors from operation failures. </summary>
        /// <param name="failures"> The operation failures to map. </param>
        /// <returns> The mapped IPC errors. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failures" /> is <see langword="null" />. </exception>
        private static IpcError[] CreateErrors (IReadOnlyList<OperationFailure> failures)
        {
            if (failures == null)
            {
                throw new ArgumentNullException(nameof(failures));
            }

            var errors = new IpcError[failures.Count];
            for (var i = 0; i < failures.Count; i++)
            {
                var failure = failures[i];
                errors[i] = new IpcError(failure.Code, failure.Message, failure.OpId);
            }

            return errors;
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
    }
}
