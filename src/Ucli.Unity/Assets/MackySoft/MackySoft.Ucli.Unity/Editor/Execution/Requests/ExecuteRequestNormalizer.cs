using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Validates and normalizes execute request payloads into strict contract models. </summary>
    internal sealed class ExecuteRequestNormalizer : IExecuteRequestNormalizer
    {
        private readonly ExecuteRequestCompiler requestCompiler = new();

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

            if (!IpcRequestContractReader.TryRead(
                requestObject: request.Arguments,
                profile: IpcRequestContractReadProfile.StrictExecute,
                requestContract: out var parsedContract,
                error: out var readError))
            {
                return ExecuteRequestNormalizationResult.Failure(MapReadError(readError));
            }

            if (parsedContract.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.ProtocolVersionMismatch(
                    expectedVersion: IpcProtocol.CurrentVersion,
                    actualVersion: parsedContract.ProtocolVersion));
            }

            if (parsedContract.RequestId is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'requestId' is required.",
                    opId: null));
            }

            if (parsedContract.Steps is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'steps' is required.",
                    opId: null));
            }

            var validatedSteps = new List<IpcRequestContractStep>(parsedContract.Steps.Count);
            foreach (var step in parsedContract.Steps)
            {
                if (step is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Step must be an object.",
                        opId: null));
                }

                if (step.Id is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Step id is required.",
                        opId: null));
                }

                if (step.Kind is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Step kind is required.",
                        opId: step.Id));
                }

                validatedSteps.Add(step);
            }

            var canonicalPayload = CanonicalRequestWriter.WriteDigestPayload(parsedContract.ProtocolVersion, validatedSteps);
            var normalizedPlanToken = StringValueNormalizer.TrimToNull(request.PlanToken);
            if (!requestCompiler.TryPrepareSourceSteps(
                parsedContract,
                out var sourceSteps,
                out var compileError))
            {
                return ExecuteRequestNormalizationResult.Failure(compileError);
            }

            var normalizedRequest = new NormalizedExecuteRequest(
                ProtocolVersion: parsedContract.ProtocolVersion,
                RequestId: parsedContract.RequestId,
                SourceSteps: sourceSteps,
                PlanToken: normalizedPlanToken,
                CanonicalDigestPayloadUtf8: canonicalPayload);
            return ExecuteRequestNormalizationResult.Success(normalizedRequest);
        }

        private static ExecuteRequestNormalizationError MapReadError (in IpcRequestContractReadError readError)
        {
            if (readError.Kind == IpcRequestContractReadErrorKind.StepEditContractViolation)
            {
                return ExecuteRequestNormalizationError.InvalidArgument(
                    message: readError.DiagnosticMessage ?? "Request arguments are invalid.",
                    opId: readError.StepId);
            }

            var violation = IpcRequestContractViolationClassifier.Classify(readError);
            var stepId = violation.StepId ?? string.Empty;
            return violation.Kind switch
            {
                IpcRequestContractViolationKind.RequestMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments must be a JSON object.",
                    null),
                IpcRequestContractViolationKind.UnknownRequestProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Request contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.ProtocolVersionMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' is required.",
                    null),
                IpcRequestContractViolationKind.ProtocolVersionTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' must be an integer.",
                    null),
                IpcRequestContractViolationKind.RequestIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' is required.",
                    null),
                IpcRequestContractViolationKind.RequestIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be a UUID string.",
                    null),
                IpcRequestContractViolationKind.RequestIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.RequestIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.RequestIdFormatMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be UUID format 'D'.",
                    null),
                IpcRequestContractViolationKind.StepsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'steps' is required.",
                    null),
                IpcRequestContractViolationKind.StepsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'steps' must be an array.",
                    null),
                IpcRequestContractViolationKind.StepMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} must be an object.",
                    null),
                IpcRequestContractViolationKind.StepKindMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' is required.",
                    null),
                IpcRequestContractViolationKind.StepKindTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must be a string.",
                    null),
                IpcRequestContractViolationKind.StepKindEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must not be empty.",
                    null),
                IpcRequestContractViolationKind.StepKindOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.StepKindUnsupported => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' is unsupported: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.UnknownStepProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.StepIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' is required.",
                    null),
                IpcRequestContractViolationKind.StepIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must be a string.",
                    null),
                IpcRequestContractViolationKind.StepIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must not be empty.",
                    null),
                IpcRequestContractViolationKind.StepIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.StepOpMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepOpTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must be a string.",
                    stepId),
                IpcRequestContractViolationKind.StepOpEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must not be empty.",
                    stepId),
                IpcRequestContractViolationKind.StepOpOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must not contain leading or trailing whitespace.",
                    stepId),
                IpcRequestContractViolationKind.StepArgsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'args' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepArgsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'args' must be an object.",
                    stepId),
                IpcRequestContractViolationKind.StepOnMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'on' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepOnTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'on' must be an object.",
                    stepId),
                IpcRequestContractViolationKind.StepSelectMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'select' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepSelectTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'select' must be an object.",
                    stepId),
                IpcRequestContractViolationKind.StepActionsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepActionsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' must be an array.",
                    stepId),
                IpcRequestContractViolationKind.StepActionMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' must contain only objects.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must be a string.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must not be empty.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must not contain leading or trailing whitespace.",
                    stepId),
                IpcRequestContractViolationKind.DuplicatedStepId => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step id is duplicated: {violation.DuplicatedStepId}.",
                    violation.DuplicatedStepId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments are invalid.",
                    null),
            };
        }
    }
}
