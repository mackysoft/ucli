using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Validates and normalizes execute request payloads into strict contract models. </summary>
    internal sealed class ExecuteRequestNormalizer : IExecuteRequestNormalizer
    {
        private static readonly HashSet<string> AllowedRequestProperties = new(StringComparer.Ordinal)
        {
            "protocolVersion",
            "requestId",
            "ops",
        };

        /// <summary> Validates and normalizes one execute request payload. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The normalization result that contains either normalized request data or one structured error. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public ExecuteRequestNormalizationResult Normalize (
            IpcExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!IpcExecuteCommandNames.IsOperationPipelineCommand(request.Command))
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Execute command is not supported: {request.Command}.",
                    opId: null));
            }

            if (request.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request arguments must be a JSON object.",
                    opId: null));
            }

            var unknownRequestProperty = JsonPropertyGuard.FindUnknownProperty(request.Arguments, AllowedRequestProperties);
            if (unknownRequestProperty is not null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Request contains an unknown property: {unknownRequestProperty}.",
                    opId: null));
            }

            if (!ExecuteRequestHeaderReader.TryReadProtocolVersion(request.Arguments, out var protocolVersion, out var protocolVersionError))
            {
                return ExecuteRequestNormalizationResult.Failure(protocolVersionError!);
            }

            if (!ExecuteRequestHeaderReader.TryReadRequestId(request.Arguments, out var requestId, out var requestIdError))
            {
                return ExecuteRequestNormalizationResult.Failure(requestIdError!);
            }

            if (!ExecuteRequestOperationReader.TryReadOperations(request.Arguments, cancellationToken, out var operations, out var operationsError))
            {
                return ExecuteRequestNormalizationResult.Failure(operationsError!);
            }

            var canonicalPayload = CanonicalRequestWriter.WriteDigestPayload(protocolVersion, operations);
            var normalizedRequest = new NormalizedExecuteRequest(
                ProtocolVersion: protocolVersion,
                RequestId: requestId,
                Ops: operations,
                CanonicalDigestPayloadUtf8: canonicalPayload);
            return ExecuteRequestNormalizationResult.Success(normalizedRequest);
        }
    }
}
